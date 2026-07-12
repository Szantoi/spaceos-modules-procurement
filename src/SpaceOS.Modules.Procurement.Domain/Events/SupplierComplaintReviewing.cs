using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintReviewing(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string ReviewedBy) : IDomainEvent;
