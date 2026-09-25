using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nidhi.Application.Authentication;
using Nidhi.Infrastructure.Identity;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Api.Authentication;

public static class AuthConfiguration
{
    public static void AddNidhiAuthentication(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddProblemDetails(options => options.CustomizeProblemDetails = AuthProblems.CompleteFrameworkProblem);
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<NidhiDbContext>().AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".Nidhi.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = ctx => AuthProblems.WriteAsync(ctx.HttpContext, 401, "AUTHENTICATION_REQUIRED", "Authentication is required.");
            options.Events.OnRedirectToAccessDenied = ctx => AuthProblems.WriteAsync(ctx.HttpContext, 403, "FORBIDDEN", "Access is forbidden.");
            // Check the real store on every request; do not renew the fixed eight-hour ticket.
            options.Events.OnValidatePrincipal = async ctx =>
            {
                var manager = ctx.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
                if (await manager.ValidateSecurityStampAsync(ctx.Principal) is null)
                {
                    ctx.RejectPrincipal();
                    await manager.SignOutAsync();
                }
            };
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = ".Nidhi.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.Customer, policy => policy.RequireAuthenticatedUser().RequireRole("CUSTOMER"));
            options.AddPolicy(AuthPolicies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole("ADMIN"));
            options.AddPolicy(AuthPolicies.VerifiedCustomer, policy => policy.RequireAuthenticatedUser().RequireRole("CUSTOMER").AddRequirements(new VerifiedEmailRequirement()));
        });
        services.AddScoped<IAuthorizationHandler, VerifiedEmailHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthAuthorizationResultHandler>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityEmailSender, LocalIdentityEmailSender>();
        services.AddScoped<AdminProvisioner>();
        services.PostConfigure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ctx =>
        {
            // Do not echo JSON input or framework exception text (it may contain secrets).
            var errors = ctx.ModelState.Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, _ => new[] { "The field is missing, unsupported, or invalid." });
            return new ObjectResult(AuthProblems.Create(ctx.HttpContext, 400, "VALIDATION_ERROR", "The request failed validation.", errors))
                { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        });
        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (ctx, _) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    ctx.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await AuthProblems.WriteAsync(ctx.HttpContext, 429, "RATE_LIMITED", "Too many requests. Please retry later.");
            };
            foreach (var (name, defaultLimit) in new[] { ("register", 5), ("login", 10), ("forgot-password", 5), ("tokens", 20) })
            {
                var limit = builder.Configuration.GetValue<int?>($"Auth:RateLimits:{name}:PermitLimit") ?? defaultLimit;
                var seconds = builder.Configuration.GetValue<int?>($"Auth:RateLimits:{name}:WindowSeconds") ?? 60;
                if (limit < 1 || seconds < 1) throw new InvalidOperationException("Auth rate limits must be positive.");
                options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = limit, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0, AutoReplenishment = true }));
            }
        });
    }
}
