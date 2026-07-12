using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Receipt queue entry for offline-first ASN scanning sync
/// </summary>
public class ReceiptQueue : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AsnShipmentId { get; private set; }
    public Guid ScannedBy { get; private set; }
    public int ActualQuantity { get; private set; }
    public DateTime ScannedAt { get; private set; }
    public DateTime? SyncedAt { get; private set; }
    public ReceiptStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ReceiptQueue() { }

    public static ReceiptQueue Create(
        Guid tenantId,
        Guid asnShipmentId,
        Guid scannedBy,
        int actualQuantity,
        DateTime scannedAt)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (asnShipmentId == Guid.Empty) throw new ArgumentException("AsnShipmentId required.", nameof(asnShipmentId));
        if (scannedBy == Guid.Empty) throw new ArgumentException("ScannedBy required.", nameof(scannedBy));
        if (actualQuantity <= 0) throw new ArgumentException("ActualQuantity must be positive.", nameof(actualQuantity));

        return new ReceiptQueue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AsnShipmentId = asnShipmentId,
            ScannedBy = scannedBy,
            ActualQuantity = actualQuantity,
            ScannedAt = scannedAt,
            Status = ReceiptStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsSynced()
    {
        if (Status == ReceiptStatus.Synced)
            throw new InvalidOperationException("Receipt is already synced.");

        Status = ReceiptStatus.Synced;
        SyncedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = ReceiptStatus.Failed;
    }
}
