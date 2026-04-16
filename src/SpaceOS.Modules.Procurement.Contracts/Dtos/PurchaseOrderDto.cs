namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>A purchase order for panel material sent to a supplier.</summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    string Status,
    DateTime? ExpectedDelivery);
