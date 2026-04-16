using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseOrderSubmittedEvent(
    Guid OrderId,
    Guid TenantId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity) : IDomainEvent;
