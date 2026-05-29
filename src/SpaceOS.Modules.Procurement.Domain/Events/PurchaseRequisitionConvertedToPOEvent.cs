using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record PurchaseRequisitionConvertedToPOEvent(
    Guid RequisitionId,
    Guid TenantId,
    Guid PurchaseOrderId) : IDomainEvent;
