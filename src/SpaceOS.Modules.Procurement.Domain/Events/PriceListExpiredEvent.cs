using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PriceListExpiredEvent(
    Guid PriceListId,
    Guid TenantId) : IDomainEvent;
