namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Transactional outbox entity for Procurement module.
/// Lifecycle: Pending → InFlight → Completed | Failed.
/// ADR-039: inserted in same transaction as domain mutation (BE-P-01).
/// </summary>
public sealed class ProcurementOutboxMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string MessageType { get; private set; } = default!;
    public int SchemaVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string PayloadJson { get; private set; } = default!;
    public string Status { get; private set; } = "Pending";
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private ProcurementOutboxMessage() { }

    /// <summary>Creates a new Pending outbox message.</summary>
    public static ProcurementOutboxMessage Create(
        Guid tenantId,
        string messageType,
        Guid idempotencyKey,
        string payloadJson,
        int schemaVersion = 1) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        MessageType = messageType,
        SchemaVersion = schemaVersion,
        IdempotencyKey = idempotencyKey,
        PayloadJson = payloadJson,
        Status = "Pending",
        AttemptCount = 0,
        NextAttemptAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>Marks the message as in-flight with a lease expiry.</summary>
    public void MarkInFlight(int leaseSeconds = 60)
    {
        Status = "InFlight";
        AttemptCount++;
        LeaseUntil = DateTimeOffset.UtcNow.AddSeconds(leaseSeconds);
    }

    /// <summary>Marks the message as successfully completed.</summary>
    public void MarkCompleted()
    {
        Status = "Completed";
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a failure. Permanent failures (4xx) are set immediately to Failed.
    /// Transient failures use exponential back-off up to maxAttempts.
    /// </summary>
    public void RecordFailure(string errorType, int maxAttempts, bool isPermanent = false)
    {
        LastError = errorType;
        if (isPermanent || AttemptCount >= maxAttempts)
        {
            Status = "Failed";
        }
        else
        {
            Status = "Pending";
            NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, AttemptCount) * 5);
        }
    }
}
