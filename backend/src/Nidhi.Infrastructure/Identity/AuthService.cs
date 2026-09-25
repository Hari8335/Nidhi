using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nidhi.Application.Authentication;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;
using Npgsql;

namespace Nidhi.Infrastructure.Identity;

public sealed class AuthService(NidhiDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IIdentityEmailSender email, ILogger<AuthService> logger) : IAuthService
{
    public async Task<RegistrationResponse> RegisterAsync(RegisterRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (await users.FindByEmailAsync(request.Email) is not null) throw DuplicateEmail();
            await IdentityRoles.EnsureAsync(db);
            var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            Check(await users.CreateAsync(user, request.Password));
            Check(await users.AddToRoleAsync(user, "CUSTOMER"));
            var now = DateTime.UtcNow;
            db.CustomerProfiles.Add(new CustomerProfile(user.Id, request.DisplayName, now, null));
            db.Wallets.Add(new Wallet(user.Id, 0.00m, now, null));
            db.GoldHoldings.Add(new GoldHolding(user.Id, 0.00000000m, now, null));
            await db.SaveChangesAsync();
            // Delivery failure rolls registration back, allowing the same registration to be retried.
            await email.SendVerificationAsync(user.Id, user.Email!, await users.GenerateEmailConfirmationTokenAsync(user));
            await transaction.CommitAsync();
            return new(user.Id, user.Email!, request.DisplayName, false,
                "Registration successful. Please verify your email before using simulated financial features.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "EmailIndex" or "UserNameIndex" })
        {
            throw DuplicateEmail();
        }
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || user.EmailConfirmed) throw InvalidToken();
        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded) throw InvalidToken();
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Run the built-in password hasher even for an unknown account.
            _ = users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            throw InvalidCredentials();
        }
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) throw InvalidCredentials();
        var roles = await users.GetRolesAsync(user);
        if (roles.Count != 1 || (roles[0] != "CUSTOMER" && roles[0] != "ADMIN")) throw InvalidCredentials();
        await signIn.SignInAsync(user, isPersistent: false);
        return new(user.Id, user.Email!, roles[0], user.EmailConfirmed);
    }

    public async Task LogoutAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        if (user is not null) Check(await users.UpdateSecurityStampAsync(user));
        await signIn.SignOutAsync();
    }

    public async Task<CurrentUserResponse> CurrentUserAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal)
            ?? throw new AuthFailure("AUTHENTICATION_REQUIRED", 401, "Authentication is required.");
        var profile = await db.CustomerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == user.Id);
        return new(user.Id, user.Email!, profile?.DisplayName, user.EmailConfirmed, (await users.GetRolesAsync(user)).ToArray());
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return;
        try
        {
            await email.SendPasswordResetAsync(user.Id, user.Email!, await users.GeneratePasswordResetTokenAsync(user));
        }
        catch (Exception)
        {
            // No exception object, address, or token: delivery outcomes must not enumerate accounts.
            logger.LogWarning("Identity password recovery delivery failed.");
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null) throw InvalidToken();
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (result.Errors.Any(x => x.Code is "InvalidToken" or "ConcurrencyFailure")) throw InvalidToken();
        Check(result);
    }

    internal static void Check(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName")) throw DuplicateEmail();
        var passwordErrors = result.Errors.Where(x => x.Code.StartsWith("Password", StringComparison.Ordinal)).Select(x => x.Description).ToArray();
        throw new AuthFailure("VALIDATION_ERROR", 400, "The identity request could not be completed.",
            new Dictionary<string, string[]> { [passwordErrors.Length > 0 ? "password" : "request"] = passwordErrors.Length > 0 ? passwordErrors : ["Check the supplied identity details and retry."] });
    }

    private static AuthFailure DuplicateEmail() => new("EMAIL_ALREADY_EXISTS", 409, "This email is already registered.");
    private static AuthFailure InvalidToken() => new("INVALID_TOKEN", 400, "The token is invalid, expired, or already used.");
    private static AuthFailure InvalidCredentials() => new("INVALID_CREDENTIALS", 401, "The email or password is incorrect.");
}

internal static class IdentityRoles
{
    public static async Task EnsureAsync(NidhiDbContext db)
    {
        foreach (var role in new[] { "CUSTOMER", "ADMIN" })
        {
            var id = Guid.CreateVersion7();
            var stamp = Guid.NewGuid().ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO asp_net_roles (id, name, normalized_name, concurrency_stamp) VALUES ({id}, {role}, {role}, {stamp}) ON CONFLICT (normalized_name) DO NOTHING");
        }
    }
}
