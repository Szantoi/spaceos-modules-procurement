using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Interfaces;

/// <summary>
/// Extended repository interface for Procurement v2 aggregates.
/// All write operations use a shared DbContext for BE-P-01 UoW atomicity.
/// </summary>
public interface IProcurementV2Repository
{
    // ── PurchaseRequisition ──────────────────────────────────────────────────
    Task<PurchaseRequisition?> GetRequisitionByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseRequisition>> GetRequisitionsByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddRequisitionAsync(PurchaseRequisition requisition, CancellationToken ct = default);
    Task<bool> RequisitionSourceRefExistsAsync(Guid tenantId, Guid sourceReference, CancellationToken ct = default);

    // ── SupplierInvoice ──────────────────────────────────────────────────────
    Task<SupplierInvoice?> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierInvoice>> GetInvoicesByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddInvoiceAsync(SupplierInvoice invoice, CancellationToken ct = default);
    Task<bool> InvoiceNumberExistsAsync(Guid tenantId, Guid supplierId, string invoiceNumber, CancellationToken ct = default);

    // ── InvoiceMatch ─────────────────────────────────────────────────────────
    Task AddInvoiceMatchAsync(InvoiceMatchEntity match, CancellationToken ct = default);

    // ── PriceList ────────────────────────────────────────────────────────────
    Task<PriceList?> GetPriceListByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PriceList>> GetPriceListsByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PriceList>> GetPriceListsBySupplierAsync(Guid tenantId, Guid supplierId, CancellationToken ct = default);
    Task<IReadOnlyList<PriceList>> GetActivePriceListsBySupplierAsync(Guid tenantId, Guid supplierId, string currency, CancellationToken ct = default);
    Task AddPriceListAsync(PriceList priceList, CancellationToken ct = default);
    Task<bool> HasOverlappingActivePriceListAsync(Guid tenantId, Guid supplierId, string currency, DateOnly validFrom, DateOnly? validTo, Guid excludeId, CancellationToken ct = default);

    // ── Best Price (BE-P-06) ─────────────────────────────────────────────────
    Task<PriceListEntry?> GetBestPriceAsync(Guid tenantId, string materialCode, int quantity, string currency, DateOnly asOf, CancellationToken ct = default);

    // ── Match Policy ─────────────────────────────────────────────────────────
    Task<MatchPolicyEntity?> GetMatchPolicyAsync(Guid tenantId, CancellationToken ct = default);
    Task UpsertMatchPolicyAsync(MatchPolicyEntity policy, CancellationToken ct = default);

    // ── Match Query (BE-P-06): GROUP BY query ─────────────────────────────────
    Task<IReadOnlyDictionary<Guid, int>> GetReceivedQuantitiesByPoLineAsync(Guid purchaseOrderId, CancellationToken ct = default);

    // ── Outbox (BE-P-01) ─────────────────────────────────────────────────────
    Task AddOutboxMessageAsync(ProcurementOutboxMessage message, CancellationToken ct = default);

    // ── Inbox (Track E idempotency) ───────────────────────────────────────────
    Task<ProcurementInboxMessage?> GetInboxMessageByIdempotencyKeyAsync(Guid tenantId, string messageType, string idempotencyKey, CancellationToken ct = default);
    Task AddInboxMessageAsync(ProcurementInboxMessage message, CancellationToken ct = default);

    // ── Audit Log (BE-P-01 + SEC-P-05) ───────────────────────────────────────
    Task AddAuditLogAsync(ProcurementAuditLog entry, CancellationToken ct = default);

    // ── RequisitionNumber ─────────────────────────────────────────────────────
    Task<string> GenerateRequisitionNumberAsync(Guid tenantId, CancellationToken ct = default);

    // ── PO lines (for match query) ─────────────────────────────────────────────
    Task<IReadOnlyList<PoLineInput>> GetPoLinesAsync(Guid purchaseOrderId, CancellationToken ct = default);

    // ── Delivery.InventorySyncStatus ─────────────────────────────────────────
    Task UpdateDeliveryInventorySyncStatusAsync(Guid deliveryId, string status, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
