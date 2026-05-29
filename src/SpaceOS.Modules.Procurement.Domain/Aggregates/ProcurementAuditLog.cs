namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Financial audit log entry (append-only, SEC-P-05).
/// Written in the same transaction as the domain mutation (BE-P-01).
/// Retention: >= 7 years (accounting).
/// </summary>
public sealed class ProcurementAuditLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Actor { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string AggregateType { get; private set; } = default!;
    public Guid AggregateId { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? SourceIp { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ProcurementAuditLog() { }

    /// <summary>Creates a new audit log entry.</summary>
    public static ProcurementAuditLog Create(
        Guid tenantId,
        string actor,
        string action,
        string aggregateType,
        Guid aggregateId,
        string? beforeJson = null,
        string? afterJson = null,
        string? sourceIp = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Actor = actor,
        Action = action,
        AggregateType = aggregateType,
        AggregateId = aggregateId,
        BeforeJson = beforeJson,
        AfterJson = afterJson,
        SourceIp = sourceIp,
        CreatedAt = DateTime.UtcNow
    };
}
