using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseRequisitionCreatedEvent(
    Guid RequisitionId,
    Guid TenantId,
    RequisitionSource Source,
    Guid? SourceReference) : IDomainEvent;
