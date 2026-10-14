using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;
using Nidhi.Api.Authentication;

namespace Nidhi.Api.Controllers;

/// <summary>
/// Customer-facing financial endpoints.
/// All routes require the VerifiedCustomer policy (authenticated CUSTOMER + confirmed email)
/// except the dashboard and wallet summary which require the Customer policy only, so that
/// unverified customers can still see their empty balances.
/// </summary>
[ApiController]
[Route("api/v1/customer")]
[Authorize(Policy = AuthPolicies.Customer)]
[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(401, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(403, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(500, "application/problem+json")]
public sealed class CustomerController(IWalletService walletService) : ControllerBase
{
    // ── GET /api/v1/customer/dashboard ────────────────────────────────────

    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardResponse>(200)]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken ct)
    {
        try { return await walletService.GetDashboardAsync(User, ct); }
        catch (Exception ex) when (ex is not WalletFailure) { throw; }
    }

    // ── GET /api/v1/customer/wallet ───────────────────────────────────────

    [HttpGet("wallet")]
    [ProducesResponseType<WalletSummaryResponse>(200)]
    public async Task<ActionResult<WalletSummaryResponse>> GetWallet(CancellationToken ct)
    {
        try { return await walletService.GetWalletAsync(User, ct); }
        catch (Exception ex) when (ex is not WalletFailure) { throw; }
    }

    // ── POST /api/v1/customer/wallet/fundings ─────────────────────────────

    [HttpPost("wallet/fundings")]
    [Authorize(Policy = AuthPolicies.VerifiedCustomer)]
    [ProducesResponseType<FundingReceiptResponse>(201)]
    [ProducesResponseType<FundingReceiptResponse>(200)]   // idempotent replay
    [ProducesResponseType<ProblemDetails>(409, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(422, "application/problem+json")]
    public async Task<IActionResult> FundWallet(
        [FromBody] FundWalletRequest request,
        CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new ObjectResult(AuthProblems.Create(HttpContext, 400, "VALIDATION_ERROR",
                "The Idempotency-Key header is required."))
            { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        }

        // Enforce max key length to prevent oversized inputs.
        if (idempotencyKey.Length > 128)
        {
            return new ObjectResult(AuthProblems.Create(HttpContext, 400, "VALIDATION_ERROR",
                "The Idempotency-Key header must not exceed 128 characters."))
            { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        }

        try
        {
            var (receipt, isReplay) = await walletService.FundWalletAsync(User, request, idempotencyKey, ct);
            return isReplay ? Ok(receipt) : StatusCode(201, receipt);
        }
        catch (WalletFailure failure)
        {
            return new ObjectResult(AuthProblems.Create(HttpContext, failure.Status, failure.Code, failure.Message))
            { StatusCode = failure.Status, ContentTypes = { "application/problem+json" } };
        }
    }
}
