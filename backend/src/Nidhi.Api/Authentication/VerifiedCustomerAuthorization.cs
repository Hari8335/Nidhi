using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Identity;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Api.Authentication;

public sealed class VerifiedEmailRequirement : IAuthorizationRequirement;

public sealed class VerifiedEmailHandler(UserManager<ApplicationUser> users) : AuthorizationHandler<VerifiedEmailRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, VerifiedEmailRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("CUSTOMER"))
        {
            var user = await users.GetUserAsync(context.User);
            if (user?.EmailConfirmed == true) context.Succeed(requirement);
        }
    }
}

public sealed class AuthAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Forbidden && context.User.IsInRole("CUSTOMER") &&
            result.AuthorizationFailure?.FailedRequirements.Any(x => x is VerifiedEmailRequirement) == true)
            return AuthProblems.WriteAsync(context, 403, "EMAIL_NOT_VERIFIED", "Email verification is required.");
        return fallback.HandleAsync(next, context, policy, result);
    }
}
