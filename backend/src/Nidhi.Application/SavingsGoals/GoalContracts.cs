using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Nidhi.Domain.Entities;

namespace Nidhi.Application.SavingsGoals;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateGoalRequest(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal TargetGrams,
    DateTime? TargetDateUtc = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (TargetGrams <= 0 || TargetGrams >= 1_000_000_000_000m ||
            (decimal.GetBits(TargetGrams)[3] >> 16 & 255) > 8)
            yield return new("Enter a positive gram target with at most 12 integer and 8 decimal places.", [nameof(TargetGrams)]);
        if (TargetDateUtc is { Kind: not DateTimeKind.Utc })
            yield return new("Target date must be a UTC timestamp ending in Z.", [nameof(TargetDateUtc)]);
    }
}

public sealed record GoalResponse(Guid GoalId, decimal TargetGrams, DateTime? TargetDateUtc,
    decimal CurrentHoldingGrams, decimal ProgressRatio, decimal ProgressPercentage,
    decimal VisualProgressPercentage, bool IsCompleted, DateTime CreatedAtUtc)
{
    public static GoalResponse From(SavingsGoal goal, decimal holding)
    {
        // Divide before multiplying: the full numeric(20,8) range fits decimal even at the smallest target.
        var ratio = holding / goal.TargetGrams;
        var percentage = ratio * 100m;
        return new(goal.GoalId, goal.TargetGrams, goal.TargetDateUtc, holding, ratio,
            percentage, Math.Min(percentage, 100m), holding >= goal.TargetGrams, goal.CreatedAtUtc);
    }
}

public interface ISavingsGoalService
{
    Task<GoalResponse?> GetAsync(ClaimsPrincipal principal, CancellationToken ct);
    Task<GoalResponse> CreateAsync(ClaimsPrincipal principal, CreateGoalRequest request, CancellationToken ct);
}
