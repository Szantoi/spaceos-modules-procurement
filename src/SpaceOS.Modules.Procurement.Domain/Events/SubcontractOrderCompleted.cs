using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SubcontractOrderCompleted(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    DateTime CompletedAt) : IDomainEvent;
