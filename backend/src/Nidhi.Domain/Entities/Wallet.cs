namespace Nidhi.Domain.Entities;

public sealed class Wallet
{
    private Wallet() { }

    public Wallet(Guid customerId, decimal balanceLkr, DateTime createdAtUtc, DateTime? updatedAtUtc)
    {
        CustomerId = customerId;
        BalanceLkr = balanceLkr;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid WalletId { get; private set; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public decimal BalanceLkr { get; private set; }
    public uint ConcurrencyToken { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
}
