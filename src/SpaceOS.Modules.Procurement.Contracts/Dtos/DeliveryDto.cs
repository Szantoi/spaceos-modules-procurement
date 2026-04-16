namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Records a physical delivery received against a purchase order.</summary>
public sealed record DeliveryDto(
    Guid Id,
    Guid PurchaseOrderId,
    string MaterialType,
    decimal Thickness,
    int PanelCount,
    DateTime ReceivedAt,
    string? Notes);
