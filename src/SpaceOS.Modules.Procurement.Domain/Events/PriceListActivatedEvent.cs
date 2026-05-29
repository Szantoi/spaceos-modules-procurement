using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PriceListActivatedEvent(
    Guid PriceListId,
    Guid TenantId,
    Guid SupplierId,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo) : IDomainEvent;
