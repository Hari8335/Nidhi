using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nidhi.Api.Authentication;
using Nidhi.Application.Authentication;
using Nidhi.Application.CustomerRecords;
using Nidhi.Application.Wallet;

namespace Nidhi.Api.Controllers;

[ApiController]
[Route("api/v1/customer")]
[Authorize(Policy = AuthPolicies.VerifiedCustomer)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerRecordsController(ICustomerReadService reads) : ControllerBase
{
    [HttpGet("holdings")]
    public Task<HoldingsResponse> Holdings(CancellationToken ct) => reads.GetHoldingsAsync(User, ct);

    [HttpGet("transactions")]
    public Task<TransactionPageResponse> Transactions([FromQuery] TransactionPageRequest pagination, CancellationToken ct) =>
        reads.GetTransactionsAsync(User, pagination, ct);

    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType<FundingReceiptResponse>(200)]
    [ProducesResponseType<GoldPurchaseReceiptResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404, "application/problem+json")]
    public async Task<IActionResult> Transaction(Guid id, CancellationToken ct)
    {
        try { return Ok(await reads.GetTransactionAsync(User, id, ct)); }
        catch (WalletFailure f)
        {
            return new ObjectResult(AuthProblems.Create(HttpContext, f.Status, f.Code, f.Message))
                { StatusCode = f.Status, ContentTypes = { "application/problem+json" } };
        }
    }
}
