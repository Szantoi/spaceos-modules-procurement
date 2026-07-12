using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintResponded(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    ResponseType ResponseType,
    string RespondedBy) : IDomainEvent;
