using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierComplaintCreated(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    Guid DeliveryId,
    ComplaintType Type,
    string Subject) : IDomainEvent;
