namespace Nidhi.Domain.Entities;

public sealed class GoldHolding
{
    private GoldHolding() { }

    public GoldHolding(Guid customerId, decimal quantityGrams, DateTime createdAtUtc, DateTime? updatedAtUtc)
    {
        CustomerId = customerId;
        QuantityGrams = quantityGrams;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid HoldingId { get; private set; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public decimal QuantityGrams { get; private set; }
    public uint ConcurrencyToken { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
}
