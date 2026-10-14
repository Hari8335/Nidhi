using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nidhi.Api.Authentication;
using Nidhi.Application.Authentication;
using Nidhi.Application.SavingsGoals;
using Nidhi.Application.Wallet;

namespace Nidhi.Api.Controllers;

[ApiController]
[Route("api/v1/customer/savings-goal")]
[Authorize(Policy = AuthPolicies.VerifiedCustomer)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SavingsGoalController(ISavingsGoalService goals) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try { var goal = await goals.GetAsync(User, ct); return goal is null ? Ok(new { goal = (object?)null }) : Ok(goal); }
        catch (WalletFailure f) { return Failure(f); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateGoalRequest request, CancellationToken ct)
    {
        try { return Created("/api/v1/customer/savings-goal", await goals.CreateAsync(User, request, ct)); }
        catch (WalletFailure f) { return Failure(f); }
    }

    private ObjectResult Failure(WalletFailure f) => new(AuthProblems.Create(HttpContext, f.Status, f.Code, f.Message))
        { StatusCode = f.Status, ContentTypes = { "application/problem+json" } };
}
