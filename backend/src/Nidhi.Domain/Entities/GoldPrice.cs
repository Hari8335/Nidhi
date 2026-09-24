namespace Nidhi.Domain.Entities;

public sealed class GoldPrice
{
    private GoldPrice() { }

    public GoldPrice(
        decimal pricePerGramLkr,
        DateTime publishedAtUtc,
        Guid publishedByAdminId,
        string reason,
        Guid auditLogId)
    {
        PricePerGramLkr = pricePerGramLkr;
        PublishedAtUtc = publishedAtUtc;
        PublishedByAdminId = publishedByAdminId;
        Reason = reason;
        AuditLogId = auditLogId;
    }

    public Guid PriceVersionId { get; private set; } = Guid.CreateVersion7();
    public decimal PricePerGramLkr { get; private set; }
    public DateTime PublishedAtUtc { get; private set; }
    public Guid PublishedByAdminId { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid AuditLogId { get; private set; }
}
