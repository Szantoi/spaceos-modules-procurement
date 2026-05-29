using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseRequisitionRejectedEvent(
    Guid RequisitionId,
    Guid TenantId,
    string Reason) : IDomainEvent;
