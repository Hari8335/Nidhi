namespace Nidhi.Domain.Entities;

public sealed class AuditEvent
{
    private AuditEvent() { }

    public AuditEvent(
        Guid actorId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string detailsJson,
        DateTime timestampUtc)
    {
        ActorId = actorId;
        ActorRole = actorRole;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        DetailsJson = detailsJson;
        TimestampUtc = timestampUtc;
    }

    public Guid EventId { get; private set; } = Guid.CreateVersion7();
    public Guid ActorId { get; private set; }
    public string ActorRole { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public string EntityId { get; private set; } = null!;
    public string DetailsJson { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
}
