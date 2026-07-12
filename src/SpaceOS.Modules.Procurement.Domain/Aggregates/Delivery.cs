using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>Append-only record of a received delivery. No UPDATE, no DELETE.</summary>
public class Delivery
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public string? Notes { get; private set; }
    public string RecordedBy { get; private set; } = string.Empty;

    // Quality Inspection (added for complaint flow)
    public QualityInspectionResult? QualityInspection { get; private set; }
    public DateTime? InspectedAt { get; private set; }
    public string? InspectedBy { get; private set; }

    private Delivery() { }

    public static Delivery Record(Guid tenantId, Guid purchaseOrderId, decimal receivedQuantity,
        DateTime receivedAt, string? notes, string recordedBy)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("PurchaseOrderId required.", nameof(purchaseOrderId));
        if (receivedQuantity <= 0) throw new ArgumentException("ReceivedQuantity must be positive.", nameof(receivedQuantity));
        ArgumentException.ThrowIfNullOrWhiteSpace(recordedBy);

        return new Delivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseOrderId = purchaseOrderId,
            ReceivedQuantity = receivedQuantity,
            ReceivedAt = receivedAt,
            Notes = notes,
            RecordedBy = recordedBy
        };
    }

    /// <summary>
    /// Records quality inspection results for this delivery.
    /// Can only be called once per delivery.
    /// </summary>
    public void RecordQualityInspection(QualityInspectionResult inspectionResult, string inspectedBy)
    {
        ArgumentNullException.ThrowIfNull(inspectionResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(inspectedBy);

        if (QualityInspection is not null)
            throw new InvalidOperationException("Quality inspection already recorded for this delivery.");

        QualityInspection = inspectionResult;
        InspectedAt = DateTime.UtcNow;
        InspectedBy = inspectedBy;
    }
}
