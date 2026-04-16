using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

public class PurchaseOrder : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string MaterialType { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PurchaseOrderStatus Status { get; private set; }
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PurchaseOrder() { }

    public static PurchaseOrder Create(Guid tenantId, Guid supplierId, string materialType, decimal quantity,
        decimal unitPrice, string currency, DateTime? expectedDeliveryDate)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (supplierId == Guid.Empty) throw new ArgumentException("SupplierId required.", nameof(supplierId));
        ArgumentException.ThrowIfNullOrWhiteSpace(materialType);
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice <= 0) throw new ArgumentException("UnitPrice must be positive.", nameof(unitPrice));

        return new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            MaterialType = materialType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = currency ?? "HUF",
            Status = PurchaseOrderStatus.Draft,
            ExpectedDeliveryDate = expectedDeliveryDate,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot submit order in status {Status}.");
        Status = PurchaseOrderStatus.Submitted;
        RaiseDomainEvent(new PurchaseOrderSubmittedEvent(Id, TenantId, SupplierId, MaterialType, Quantity));
    }

    public void Confirm()
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException($"Cannot confirm order in status {Status}.");
        Status = PurchaseOrderStatus.Confirmed;
    }

    public void MarkShipped()
    {
        if (Status != PurchaseOrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot mark order as shipped in status {Status}.");
        Status = PurchaseOrderStatus.Shipped;
    }

    public void RecordDelivery(decimal receivedQuantity)
    {
        if (Status != PurchaseOrderStatus.Shipped)
            throw new InvalidOperationException($"Cannot record delivery in status {Status}.");
        Status = PurchaseOrderStatus.Delivered;
        RaiseDomainEvent(new PurchaseOrderDeliveredEvent(Id, TenantId, MaterialType, receivedQuantity, DateTime.UtcNow));
        RaiseDomainEvent(new ReorderAlertTriggeredEvent(TenantId, MaterialType));
    }

    public void Cancel()
    {
        if (Status == PurchaseOrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered order.");
        if (Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");
        Status = PurchaseOrderStatus.Cancelled;
    }
}
