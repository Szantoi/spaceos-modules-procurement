using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseOrderDeliveredEvent(
    Guid OrderId,
    Guid TenantId,
    string MaterialType,
    decimal ReceivedQuantity,
    DateTime DeliveredAt) : IDomainEvent;
