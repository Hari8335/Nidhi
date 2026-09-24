namespace Nidhi.Domain.Entities;

public sealed class FinancialTransaction
{
    private FinancialTransaction() { }

    public FinancialTransaction(
        Guid customerId,
        TransactionType type,
        TransactionStatus status,
        decimal amountLkr,
        decimal? goldQuantityGrams,
        Guid? priceVersionId,
        decimal? appliedPricePerGramLkr,
        decimal postOperationWalletBalanceLkr,
        decimal? postOperationGoldHoldingGrams,
        Guid idempotencyRecordId,
        DateTime createdAtUtc)
    {
        CustomerId = customerId;
        Type = type;
        Status = status;
        AmountLkr = amountLkr;
        GoldQuantityGrams = goldQuantityGrams;
        PriceVersionId = priceVersionId;
        AppliedPricePerGramLkr = appliedPricePerGramLkr;
        PostOperationWalletBalanceLkr = postOperationWalletBalanceLkr;
        PostOperationGoldHoldingGrams = postOperationGoldHoldingGrams;
        IdempotencyRecordId = idempotencyRecordId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TransactionId { get; private set; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public decimal AmountLkr { get; private set; }
    public decimal? GoldQuantityGrams { get; private set; }
    public Guid? PriceVersionId { get; private set; }
    public decimal? AppliedPricePerGramLkr { get; private set; }
    public decimal PostOperationWalletBalanceLkr { get; private set; }
    public decimal? PostOperationGoldHoldingGrams { get; private set; }
    public Guid IdempotencyRecordId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
