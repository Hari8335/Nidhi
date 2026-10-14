using Nidhi.Application.Wallet;
using Xunit;

namespace Nidhi.UnitTests;

public sealed class GoldPurchaseCalculationTests
{
    [Fact]
    public void Truncates_down_to_eight_decimals_without_rounding()
    {
        Assert.Equal(1.23456789m, GoldPurchaseCalculations.TruncateToEightDecimals(12345.67899m, 10000m));
    }

    [Fact]
    public void Exact_eight_decimal_quantity_is_preserved()
    {
        Assert.Equal(0.00333333m, GoldPurchaseCalculations.TruncateToEightDecimals(100m, 30000m));
    }

    [Fact]
    public void Quantity_below_one_eighth_decimal_is_zero()
    {
        Assert.Equal(0m, GoldPurchaseCalculations.TruncateToEightDecimals(100m, 100000000000m));
    }
}
