using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PriceListCreatedEvent(
    Guid PriceListId,
    Guid TenantId,
    Guid SupplierId,
    string Currency) : IDomainEvent;
