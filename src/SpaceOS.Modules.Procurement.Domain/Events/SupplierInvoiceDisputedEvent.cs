using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierInvoiceDisputedEvent(
    Guid InvoiceId,
    Guid TenantId,
    string Reason) : IDomainEvent;
