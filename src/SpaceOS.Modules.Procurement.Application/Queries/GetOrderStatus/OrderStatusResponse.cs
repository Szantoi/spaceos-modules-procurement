namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

public sealed record OrderStatusResponse(
    Guid Id,
    string MaterialType,
    decimal Quantity,
    string Status,
    DateTime? ExpectedDelivery);
