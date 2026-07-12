using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Advanced Shipping Notice (ASN) aggregate root for tracking supplier shipments
/// </summary>
public class AsnShipment : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AsnNumber { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateTime ExpectedDate { get; private set; }
    public string QrPayload { get; private set; } = string.Empty;
    public AsnStatus Status { get; private set; }
    public DateTime? OfflineScannedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AsnShipment() { }

    public static AsnShipment Create(
        Guid tenantId,
        string asnNumber,
        Guid purchaseOrderId,
        Guid supplierId,
        DateTime expectedDate,
        string qrPayload)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(asnNumber);
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("PurchaseOrderId required.", nameof(purchaseOrderId));
        if (supplierId == Guid.Empty) throw new ArgumentException("SupplierId required.", nameof(supplierId));
        ArgumentException.ThrowIfNullOrWhiteSpace(qrPayload);

        var now = DateTime.UtcNow;

        return new AsnShipment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AsnNumber = asnNumber,
            PurchaseOrderId = purchaseOrderId,
            SupplierId = supplierId,
            ExpectedDate = expectedDate,
            QrPayload = qrPayload,
            Status = AsnStatus.Shipped,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkAsReceived()
    {
        if (Status == AsnStatus.Received)
            throw new InvalidOperationException("ASN is already marked as received.");

        Status = AsnStatus.Received;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordOfflineScan(DateTime scannedAt)
    {
        OfflineScannedAt = scannedAt;
        Status = AsnStatus.PendingSync;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteSyncFromOffline()
    {
        if (Status != AsnStatus.PendingSync)
            throw new InvalidOperationException("Cannot complete sync - ASN is not in PendingSync status.");

        Status = AsnStatus.Received;
        UpdatedAt = DateTime.UtcNow;
    }
}
