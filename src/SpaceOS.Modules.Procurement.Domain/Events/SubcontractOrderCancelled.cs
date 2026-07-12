using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SubcontractOrderCancelled(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string Reason) : IDomainEvent;
