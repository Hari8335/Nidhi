using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Nidhi.Application.Wallet;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Incoming funding request. Amount only; all other business fields are derived
/// server-side from the authenticated session principal.
/// </summary>
public sealed record FundWalletRequest(
    [Required] decimal AmountLkr);

// ── Responses ─────────────────────────────────────────────────────────────────

/// <summary>Immutable funding receipt. Returned on first success and idempotent replays.</summary>
public sealed record FundingReceiptResponse(
    Guid TransactionId,
    string Type,
    string Status,
    decimal AmountLkr,
    decimal PostOperationWalletBalanceLkr,
    DateTime CreatedAtUtc);

/// <summary>Wallet summary including caps and daily allowance.</summary>
public sealed record WalletSummaryResponse(
    decimal BalanceLkr,
    decimal MaxBalanceCapLkr,
    decimal MinFundingAmountLkr,
    decimal MaxFundingAmountLkr,
    int SuccessfulFundingCountToday,
    int MaxDailyFundingOperations,
    int RemainingFundingOperationsToday);

/// <summary>Single transaction line-item for recent-activity views.</summary>
public sealed record TransactionSummary(
    Guid TransactionId,
    string Type,
    decimal AmountLkr,
    decimal? GoldQuantityGrams,
    decimal? AppliedPricePerGramLkr,
    string Status,
    decimal PostOperationWalletBalanceLkr,
    DateTime CreatedAtUtc);

/// <summary>Customer dashboard aggregate.</summary>
public sealed record DashboardResponse(
    decimal WalletBalanceLkr,
    decimal GoldHoldingGrams,
    IReadOnlyList<TransactionSummary> RecentTransactions,
    Nidhi.Application.SavingsGoals.GoalResponse? ActiveGoal = null);

public sealed record GoldPurchaseRequest([Required] decimal AmountLkr, [Required] Guid PriceVersionId);
public sealed record GoldPriceResponse(Guid PriceVersionId, decimal PricePerGramLkr, DateTime PublishedAtUtc, bool IsFresh, bool IsPurchasingEnabled);
public sealed record GoldPurchaseReceiptResponse(Guid TransactionId, string Type, string Status, decimal AmountLkr, Guid PriceVersionId, decimal AppliedPricePerGramLkr, decimal GoldQuantityGrams, decimal ConversionResidualLkr, decimal PostOperationWalletBalanceLkr, decimal PostOperationGoldHoldingGrams, DateTime CreatedAtUtc);

// ── Failures ──────────────────────────────────────────────────────────────────

/// <summary>Business-rule violation thrown by the wallet funding use-case.</summary>
public sealed class WalletFailure(string code, int status, string message)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

// ── Service interface ─────────────────────────────────────────────────────────

public interface IWalletService
{
    Task<WalletSummaryResponse> GetWalletAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    Task<DashboardResponse> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    /// <summary>
    /// Returns the funding receipt and whether this was an idempotent replay.
    /// IsReplay=true → respond 200 OK; IsReplay=false → respond 201 Created.
    /// </summary>
    Task<(FundingReceiptResponse Receipt, bool IsReplay)> FundWalletAsync(
        ClaimsPrincipal principal,
        FundWalletRequest request,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<GoldPriceResponse> GetGoldPriceAsync(CancellationToken ct = default);
    Task<(GoldPurchaseReceiptResponse Receipt, bool IsReplay)> PurchaseGoldAsync(ClaimsPrincipal principal, GoldPurchaseRequest request, string idempotencyKey, CancellationToken ct = default);
}
