using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintWithdrawn(
    Guid Id,
    Guid TenantId,
    string Reason,
    string WithdrawnBy) : IDomainEvent;
