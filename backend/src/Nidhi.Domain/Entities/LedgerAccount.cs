namespace Nidhi.Domain.Entities;

public sealed class LedgerAccount
{
    private LedgerAccount() { }

    public LedgerAccount(
        string accountNumber,
        string name,
        LedgerUnit unit,
        AccountClassification classification,
        Guid? customerId,
        DateTime createdAtUtc)
    {
        AccountNumber = accountNumber;
        Name = name;
        Unit = unit;
        Classification = classification;
        CustomerId = customerId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid AccountId { get; private set; } = Guid.CreateVersion7();
    public string AccountNumber { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public LedgerUnit Unit { get; private set; }
    public AccountClassification Classification { get; private set; }
    public Guid? CustomerId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
