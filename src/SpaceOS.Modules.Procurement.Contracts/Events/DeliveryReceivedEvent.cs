namespace SpaceOS.Modules.Procurement.Contracts.Events;

/// <summary>Raised when a delivery is physically received and recorded against a purchase order.</summary>
public sealed record DeliveryReceivedEvent(
    Guid TenantId,
    Guid DeliveryId,
    Guid PurchaseOrderId,
    string MaterialType,
    int PanelCount,
    DateTime OccurredAt);
