namespace Nidhi.Application.Wallet;

public static class GoldPurchaseCalculations
{
    public static decimal TruncateToEightDecimals(decimal amountLkr, decimal pricePerGramLkr)
    {
        if (amountLkr <= 0m || pricePerGramLkr <= 0m) return 0m;
        return decimal.Truncate(amountLkr / pricePerGramLkr * 100000000m) / 100000000m;
    }
}
