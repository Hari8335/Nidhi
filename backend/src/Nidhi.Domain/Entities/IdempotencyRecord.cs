namespace Nidhi.Domain.Entities;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public IdempotencyRecord(
        Guid customerId,
        IdempotencyOperation operation,
        string idempotencyKey,
        string requestHash,
        Guid? responseTransactionId,
        IdempotencyStatus status,
        DateTime createdAtUtc,
        DateTime? completedAtUtc)
    {
        CustomerId = customerId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        ResponseTransactionId = responseTransactionId;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    public Guid IdempotencyRecordId { get; private set; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public IdempotencyOperation Operation { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public Guid? ResponseTransactionId { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
}
