namespace Nidhi.Domain.Entities;

public sealed class CustomerProfile
{
    private CustomerProfile() { }

    public CustomerProfile(Guid customerId, string? displayName, DateTime createdAtUtc, DateTime? updatedAtUtc)
    {
        CustomerId = customerId;
        DisplayName = displayName;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid CustomerId { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
}
