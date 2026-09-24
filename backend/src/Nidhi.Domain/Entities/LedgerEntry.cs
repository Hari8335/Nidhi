namespace Nidhi.Domain.Entities;

public sealed class LedgerEntry
{
    private LedgerEntry() { }

    public LedgerEntry(
        Guid transactionId,
        Guid accountId,
        LedgerUnit unit,
        EntryDirection direction,
        decimal amount,
        DateTime createdAtUtc)
    {
        TransactionId = transactionId;
        AccountId = accountId;
        Unit = unit;
        Direction = direction;
        Amount = amount;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid EntryId { get; private set; } = Guid.CreateVersion7();
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public LedgerUnit Unit { get; private set; }
    public EntryDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
