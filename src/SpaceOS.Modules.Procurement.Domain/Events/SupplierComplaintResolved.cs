using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintResolved(
    Guid Id,
    Guid TenantId,
    ResolutionType ResolutionType,
    string ResolvedBy) : IDomainEvent;
