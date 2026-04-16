namespace SpaceOS.Modules.Procurement.Contracts.Events;

/// <summary>Raised when a new purchase order is submitted to a supplier.</summary>
public sealed record PurchaseOrderCreatedEvent(
    Guid TenantId,
    Guid OrderId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    DateTime OccurredAt);
