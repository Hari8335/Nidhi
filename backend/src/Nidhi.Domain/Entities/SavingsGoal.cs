namespace Nidhi.Domain.Entities;

public sealed class SavingsGoal
{
    private SavingsGoal() { }

    public SavingsGoal(
        Guid customerId,
        decimal targetGrams,
        DateTime? targetDateUtc,
        GoalStatus status,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc)
    {
        CustomerId = customerId;
        TargetGrams = targetGrams;
        TargetDateUtc = targetDateUtc;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid GoalId { get; private set; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public decimal TargetGrams { get; private set; }
    public DateTime? TargetDateUtc { get; private set; }
    public GoalStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
}
