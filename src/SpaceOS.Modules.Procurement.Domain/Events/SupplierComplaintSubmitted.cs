using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintSubmitted(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string SubmittedBy) : IDomainEvent;
