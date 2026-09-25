using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nidhi.Application.Authentication;

namespace Nidhi.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(429, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(500, "application/problem+json")]
public sealed class AuthController(IAuthService auth, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("antiforgery")]
    [ProducesResponseType<AntiforgeryResponse>(200)]
    public ActionResult<AntiforgeryResponse> Antiforgery() => new AntiforgeryResponse(antiforgery.GetAndStoreTokens(HttpContext).RequestToken!);

    [HttpPost("register"), EnableRateLimiting("register")]
    [ProducesResponseType<RegistrationResponse>(201)]
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(503, "application/problem+json")]
    public async Task<ActionResult<RegistrationResponse>> Register(RegisterRequest request) =>
        StatusCode(201, await auth.RegisterAsync(request));

    [HttpPost("verify-email"), EnableRateLimiting("tokens")]
    [ProducesResponseType<MessageResponse>(200)]
    public async Task<ActionResult<MessageResponse>> VerifyEmail(VerifyEmailRequest request)
    {
        await auth.VerifyEmailAsync(request);
        return new MessageResponse("Email successfully verified. You are now eligible to use simulated financial features.");
    }

    [HttpPost("login"), EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(200)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var result = await auth.LoginAsync(request);
        ResetAntiforgery();
        return result;
    }

    [HttpPost("logout"), Authorize]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync(User);
        ResetAntiforgery();
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    [ProducesResponseType<CurrentUserResponse>(200)]
    public async Task<ActionResult<CurrentUserResponse>> Me() => await auth.CurrentUserAsync(User);

    [HttpPost("forgot-password"), EnableRateLimiting("forgot-password")]
    [ProducesResponseType<MessageResponse>(200)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        await auth.ForgotPasswordAsync(request);
        return new MessageResponse("If the email is registered, password reset instructions have been sent.");
    }

    [HttpPost("reset-password"), EnableRateLimiting("tokens")]
    [ProducesResponseType<MessageResponse>(200)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request)
    {
        await auth.ResetPasswordAsync(request);
        ResetAntiforgery();
        return new MessageResponse("Password has been successfully reset. You may now log in.");
    }

    private void ResetAntiforgery() => Response.Cookies.Delete(".Nidhi.Antiforgery", new CookieOptions
    { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = Request.IsHttps });
}
