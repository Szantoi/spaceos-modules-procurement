using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierInvoiceReceivedEvent(
    Guid InvoiceId,
    Guid TenantId,
    Guid SupplierId,
    Guid PurchaseOrderId) : IDomainEvent;
