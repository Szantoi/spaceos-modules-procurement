using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SubcontractOrderStarted(
    Guid Id,
    Guid TenantId,
    Guid SupplierId) : IDomainEvent;
