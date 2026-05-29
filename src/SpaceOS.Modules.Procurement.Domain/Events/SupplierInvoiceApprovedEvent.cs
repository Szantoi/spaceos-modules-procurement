using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierInvoiceApprovedEvent(
    Guid InvoiceId,
    Guid TenantId,
    string Approver,
    bool ApprovedWithVariance) : IDomainEvent;
