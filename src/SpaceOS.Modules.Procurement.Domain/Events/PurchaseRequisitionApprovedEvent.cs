using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseRequisitionApprovedEvent(
    Guid RequisitionId,
    Guid TenantId,
    string ApprovedBy) : IDomainEvent;
