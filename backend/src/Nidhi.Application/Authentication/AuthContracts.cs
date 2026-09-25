using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Nidhi.Application.Authentication;

public static class AuthPolicies
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string VerifiedCustomer = "VerifiedCustomer";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [StringLength(100)] string? DisplayName = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);
public sealed record ForgotPasswordRequest([Required, EmailAddress, StringLength(256)] string Email);
public sealed record VerifyEmailRequest(Guid UserId, [Required, StringLength(4096)] string Token);
public sealed record ResetPasswordRequest(Guid UserId,
    [Required, StringLength(4096)] string Token,
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword);
public sealed record RegistrationResponse(Guid CustomerId, string Email, string? DisplayName, bool IsEmailVerified, string Message);
public sealed record LoginResponse(Guid UserId, string Email, string Role, bool IsEmailVerified);
public sealed record CurrentUserResponse(Guid UserId, string Email, string? DisplayName, bool IsEmailVerified, string[] Roles);
public sealed record MessageResponse(string Message);
public sealed record AntiforgeryResponse(string Token);

public sealed class AuthFailure(string code, int status, string message, IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

public interface IAuthService
{
    Task<RegistrationResponse> RegisterAsync(RegisterRequest request);
    Task VerifyEmailAsync(VerifyEmailRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task LogoutAsync(ClaimsPrincipal principal);
    Task<CurrentUserResponse> CurrentUserAsync(ClaimsPrincipal principal);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}

public interface IIdentityEmailSender
{
    Task SendVerificationAsync(Guid userId, string email, string token);
    Task SendPasswordResetAsync(Guid userId, string email, string token);
}
