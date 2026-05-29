namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Transactional inbox entity for Procurement module.
/// Provides idempotency for inbound integration messages.
/// Track E: receiver for reorder-alert messages from Inventory worker.
/// </summary>
public sealed class ProcurementInboxMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Message type discriminator — e.g. "ReorderAlertReceived".</summary>
    public string MessageType { get; private set; } = default!;

    /// <summary>Compound idempotency key: tenantId+messageType+alertedAt+materialCode.</summary>
    public string IdempotencyKey { get; private set; } = default!;

    /// <summary>ResultRef holds the created requisitionId once processed.</summary>
    public Guid? ResultRef { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private ProcurementInboxMessage() { }

    /// <summary>Creates a new unprocessed inbox message.</summary>
    public static ProcurementInboxMessage Create(
        Guid tenantId,
        string messageType,
        string idempotencyKey) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        MessageType = messageType,
        IdempotencyKey = idempotencyKey,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>Marks the message as processed and stores the result reference.</summary>
    public void MarkProcessed(Guid resultRef)
    {
        ResultRef = resultRef;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
