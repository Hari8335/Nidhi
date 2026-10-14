using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Nidhi.Application.SavingsGoals;
using Nidhi.Application.Wallet;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;
using Npgsql;

namespace Nidhi.Infrastructure.SavingsGoals;

public sealed class SavingsGoalService(NidhiDbContext db) : ISavingsGoalService
{
    private static Guid CustomerId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<GoalResponse?> GetAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = CustomerId(principal);
        var goal = await db.SavingsGoals.AsNoTracking().SingleOrDefaultAsync(g => g.CustomerId == id && g.Status == GoalStatus.ACTIVE, ct);
        if (goal is null) return null;
        return GoalResponse.From(goal, await Holding(id, ct));
    }

    private async Task<decimal> Holding(Guid id, CancellationToken ct) =>
        await db.GoldHoldings.AsNoTracking().Where(h => h.CustomerId == id).Select(h => (decimal?)h.QuantityGrams).SingleOrDefaultAsync(ct)
        ?? throw new WalletFailure("HOLDING_UNAVAILABLE", 503, "Your simulated holding is unavailable.");

    public async Task<GoalResponse> CreateAsync(ClaimsPrincipal principal, CreateGoalRequest request, CancellationToken ct)
    {
        var errors = request.Validate(new(request)).ToArray();
        if (errors.Length > 0) throw new WalletFailure("VALIDATION_ERROR", 400, errors[0].ErrorMessage!);
        var id = CustomerId(principal);
        var holding = await Holding(id, ct);
        var goal = new SavingsGoal(id, request.TargetGrams, request.TargetDateUtc, GoalStatus.ACTIVE, DateTime.UtcNow, null);
        db.SavingsGoals.Add(goal);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_savings_goals_one_active_per_customer" })
        {
            throw new WalletFailure("ACTIVE_GOAL_ALREADY_EXISTS", 409, "You already have an active savings goal.");
        }
        // Read the current persisted quantity again, including purchases committed during creation.
        return GoalResponse.From(goal, await Holding(id, ct));
    }
}
