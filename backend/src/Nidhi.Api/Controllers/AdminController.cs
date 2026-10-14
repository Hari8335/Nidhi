using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nidhi.Api.Authentication;
using Nidhi.Application.Admin;
using Nidhi.Application.Authentication;
using Nidhi.Application.Wallet;

namespace Nidhi.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = AuthPolicies.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("dashboard")] public Task<AdminDashboard> Dashboard(CancellationToken ct) => admin.Dashboard(ct);
    [HttpGet("customers")] public Task<AdminPage<CustomerSummary>> Customers([FromQuery] CustomerQuery query, CancellationToken ct) => admin.Customers(query, ct);
    [HttpGet("customers/{id:guid}")] public Task<IActionResult> Customer(Guid id, CancellationToken ct) => Read(() => admin.Customer(id, ct));
    [HttpGet("transactions")] public Task<AdminPage<AdminTransaction>> Transactions([FromQuery] TransactionQuery query, CancellationToken ct) => admin.Transactions(query, ct);
    [HttpGet("transactions/{id:guid}")] public Task<IActionResult> Transaction(Guid id, CancellationToken ct) => Read(() => admin.Transaction(id, ct));
    [HttpGet("transactions/{id:guid}/ledger")] public Task<IActionResult> Ledger(Guid id, CancellationToken ct) => Read(() => admin.Ledger(id, ct));
    [HttpGet("prices")] public Task<AdminPage<PricePublication>> Prices([FromQuery] AdminPageRequest query, CancellationToken ct) => admin.Prices(query, ct);
    [HttpPost("prices")] public async Task<IActionResult> Publish(PublishPriceRequest request, CancellationToken ct) => Created("/api/v1/admin/prices", await admin.Publish(User, request, ct));
    [HttpGet("audit-events")] public Task<AdminPage<AdminAudit>> Audit([FromQuery] AuditQuery query, CancellationToken ct) => admin.Audit(query, ct);
    private async Task<IActionResult> Read<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (WalletFailure f) { return new ObjectResult(AuthProblems.Create(HttpContext, f.Status, f.Code, f.Message))
            { StatusCode = f.Status, ContentTypes = { "application/problem+json" } }; }
    }
}
