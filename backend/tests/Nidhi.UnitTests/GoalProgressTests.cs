using Nidhi.Application.SavingsGoals;
using Nidhi.Domain.Entities;
using Xunit;
namespace Nidhi.UnitTests;
public sealed class GoalProgressTests
{
    [Theory]
    [InlineData(0, 0, false)] [InlineData(1, 20, false)] [InlineData(2.5, 50, false)]
    [InlineData(5, 100, true)] [InlineData(6.25, 125, true)]
    public void Progress_is_decimal_derived_and_visual_only_is_capped(decimal holding, decimal expected, bool completed)
    {
        var goal = new SavingsGoal(Guid.NewGuid(), 5m, null, GoalStatus.ACTIVE, DateTime.UtcNow, null);
        var response = GoalResponse.From(goal, holding);
        Assert.Equal(expected, response.ProgressPercentage); Assert.Equal(expected / 100m, response.ProgressRatio);
        Assert.Equal(Math.Min(expected, 100m), response.VisualProgressPercentage);
        Assert.Equal(completed, response.IsCompleted); Assert.Equal(holding, response.CurrentHoldingGrams);
        Assert.Equal(GoalStatus.ACTIVE, goal.Status);
    }
    [Fact]
    public void Entire_supported_precision_range_has_no_progress_overflow()
    {
        var goal = new SavingsGoal(Guid.NewGuid(), .00000001m, null, GoalStatus.ACTIVE, DateTime.UtcNow, null);
        Assert.Equal(9999999999999999999900m, GoalResponse.From(goal, 999999999999.99999999m).ProgressPercentage);
    }
}
