using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SubcontractOrderCreated(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string WorkDescription,
    DateTime Deadline) : IDomainEvent;
