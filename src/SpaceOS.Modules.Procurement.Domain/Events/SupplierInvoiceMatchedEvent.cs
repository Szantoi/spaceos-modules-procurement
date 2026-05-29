using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierInvoiceMatchedEvent(
    Guid InvoiceId,
    Guid TenantId,
    Guid MatchId) : IDomainEvent;
