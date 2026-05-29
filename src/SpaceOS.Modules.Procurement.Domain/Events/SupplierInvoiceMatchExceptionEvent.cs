using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Events;

public sealed record SupplierInvoiceMatchExceptionEvent(
    Guid InvoiceId,
    Guid TenantId,
    Guid MatchId,
    string VarianceSummary) : IDomainEvent;
