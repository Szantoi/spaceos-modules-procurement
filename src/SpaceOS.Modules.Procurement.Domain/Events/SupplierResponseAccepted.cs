using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierResponseAccepted(
    Guid Id,
    Guid TenantId,
    string AcceptedBy) : IDomainEvent;
