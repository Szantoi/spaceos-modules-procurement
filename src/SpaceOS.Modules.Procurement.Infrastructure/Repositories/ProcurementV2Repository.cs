using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Repositories;

/// <summary>
/// Repository for Procurement v2 aggregates.
/// All writes share the same DbContext for BE-P-01 UoW atomicity (one SaveChanges per request).
/// </summary>
public sealed class ProcurementV2Repository : IProcurementV2Repository
{
    private readonly ProcurementDbContext _db;

    public ProcurementV2Repository(ProcurementDbContext db)
    {
        _db = db;
    }

    // ── PurchaseRequisition ──────────────────────────────────────────────────

    public async Task<PurchaseRequisition?> GetRequisitionByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PurchaseRequisitions
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PurchaseRequisition>> GetRequisitionsByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.PurchaseRequisitions.AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task AddRequisitionAsync(PurchaseRequisition requisition, CancellationToken ct = default)
        => await _db.PurchaseRequisitions.AddAsync(requisition, ct).ConfigureAwait(false);

    public async Task<bool> RequisitionSourceRefExistsAsync(Guid tenantId, Guid sourceReference, CancellationToken ct = default)
        => await _db.PurchaseRequisitions.AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId && r.SourceReference == sourceReference, ct).ConfigureAwait(false);

    // ── SupplierInvoice ──────────────────────────────────────────────────────

    public async Task<SupplierInvoice?> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SupplierInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<SupplierInvoice>> GetInvoicesByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.SupplierInvoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task AddInvoiceAsync(SupplierInvoice invoice, CancellationToken ct = default)
        => await _db.SupplierInvoices.AddAsync(invoice, ct).ConfigureAwait(false);

    public async Task<bool> InvoiceNumberExistsAsync(Guid tenantId, Guid supplierId, string invoiceNumber, CancellationToken ct = default)
        => await _db.SupplierInvoices.AsNoTracking()
            .AnyAsync(i => i.TenantId == tenantId && i.SupplierId == supplierId && i.SupplierInvoiceNumber == invoiceNumber, ct).ConfigureAwait(false);

    // ── InvoiceMatch ─────────────────────────────────────────────────────────

    public async Task AddInvoiceMatchAsync(InvoiceMatchEntity match, CancellationToken ct = default)
        => await _db.InvoiceMatches.AddAsync(match, ct).ConfigureAwait(false);

    // ── PriceList ────────────────────────────────────────────────────────────

    public async Task<PriceList?> GetPriceListByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PriceLists
            .Include(pl => pl.Entries)
            .FirstOrDefaultAsync(pl => pl.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PriceList>> GetPriceListsByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.PriceLists.AsNoTracking()
            .Include(pl => pl.Entries)
            .Where(pl => pl.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PriceList>> GetPriceListsBySupplierAsync(
        Guid tenantId, Guid supplierId, CancellationToken ct = default)
        => await _db.PriceLists.AsNoTracking()
            .Include(pl => pl.Entries)
            .Where(pl => pl.TenantId == tenantId && pl.SupplierId == supplierId)
            .OrderByDescending(pl => pl.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PriceList>> GetActivePriceListsBySupplierAsync(
        Guid tenantId, Guid supplierId, string currency, CancellationToken ct = default)
        => await _db.PriceLists
            .Include(pl => pl.Entries)
            .Where(pl => pl.TenantId == tenantId
                && pl.SupplierId == supplierId
                && pl.Currency == currency
                && pl.Status == Domain.Enums.PriceListStatus.Active)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task AddPriceListAsync(PriceList priceList, CancellationToken ct = default)
        => await _db.PriceLists.AddAsync(priceList, ct).ConfigureAwait(false);

    public async Task<bool> HasOverlappingActivePriceListAsync(
        Guid tenantId, Guid supplierId, string currency,
        DateOnly validFrom, DateOnly? validTo, Guid excludeId, CancellationToken ct = default)
    {
        // DB-P-09: domain-level overlap guard
        return await _db.PriceLists.AsNoTracking()
            .Where(pl => pl.TenantId == tenantId
                && pl.SupplierId == supplierId
                && pl.Currency == currency
                && pl.Status == Domain.Enums.PriceListStatus.Active
                && pl.Id != excludeId)
            .AnyAsync(pl =>
                // overlap: not (pl.ValidTo < validFrom || pl.ValidFrom > validTo)
                (pl.ValidTo == null || pl.ValidTo >= validFrom)
                && (validTo == null || pl.ValidFrom <= validTo), ct).ConfigureAwait(false);
    }

    // ── Best Price (BE-P-06): single query ────────────────────────────────────

    public async Task<PriceListEntry?> GetBestPriceAsync(
        Guid tenantId, string materialCode, int quantity, string currency, DateOnly asOf, CancellationToken ct = default)
    {
        // BE-P-06: single Specification query — no in-memory filtering
        return await _db.PriceLists
            .AsNoTracking()
            .Where(pl => pl.TenantId == tenantId
                && pl.Currency == currency
                && pl.Status == Domain.Enums.PriceListStatus.Active
                && pl.ValidFrom <= asOf
                && (pl.ValidTo == null || pl.ValidTo >= asOf))
            .SelectMany(pl => pl.Entries)
            .Where(e => e.TenantId == tenantId
                && e.MaterialCode == materialCode
                && e.MinQuantity <= quantity
                && (e.MaxQuantity == null || e.MaxQuantity >= quantity))
            .OrderBy(e => e.UnitPrice)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    // ── Match Policy ─────────────────────────────────────────────────────────

    public async Task<MatchPolicyEntity?> GetMatchPolicyAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.MatchPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct).ConfigureAwait(false);

    public async Task UpsertMatchPolicyAsync(MatchPolicyEntity policy, CancellationToken ct = default)
    {
        var existing = await _db.MatchPolicies
            .FirstOrDefaultAsync(p => p.TenantId == policy.TenantId, ct).ConfigureAwait(false);

        if (existing is null)
        {
            await _db.MatchPolicies.AddAsync(policy, ct).ConfigureAwait(false);
        }
        else
        {
            existing.Update(policy.PriceTolerancePct, policy.QuantityToleranceAbs);
        }
    }

    // ── Match Query (BE-P-06): GROUP BY — no N+1 ─────────────────────────────

    public async Task<IReadOnlyDictionary<Guid, int>> GetReceivedQuantitiesByPoLineAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        // Since Delivery currently has no Lines entity, we use the Delivery directly
        // The match uses the delivery's ReceivedQuantity, keyed by PurchaseOrderId
        // In v1, one Delivery = one PO, so the "PO line" key maps to the delivery
        // For future DeliveryLine support, this would GROUP BY PurchaseOrderLineId
        var deliveries = await _db.Deliveries.AsNoTracking()
            .Where(d => d.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(ct).ConfigureAwait(false);

        // Use PurchaseOrderId as the "line key" since there's no PO line ID in v1 Delivery
        // Returns sum of all deliveries for this PO
        var total = (int)deliveries.Sum(d => d.ReceivedQuantity);
        return new Dictionary<Guid, int> { [purchaseOrderId] = total };
    }

    // ── PO lines (for match query) ─────────────────────────────────────────────

    public async Task<IReadOnlyList<PoLineInput>> GetPoLinesAsync(Guid purchaseOrderId, CancellationToken ct = default)
    {
        // v1 PO has no explicit line entity — the PO itself is the line
        var po = await _db.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, ct).ConfigureAwait(false);

        if (po is null)
            return Array.Empty<PoLineInput>();

        // Treat the PO as a single line using its Id as both the line and the PO ID
        return new[] { new PoLineInput(po.Id, po.MaterialType, (int)po.Quantity, po.UnitPrice) };
    }

    // ── Outbox ────────────────────────────────────────────────────────────────

    public async Task AddOutboxMessageAsync(ProcurementOutboxMessage message, CancellationToken ct = default)
        => await _db.OutboxMessages.AddAsync(message, ct).ConfigureAwait(false);

    // ── Inbox (Track E idempotency) ───────────────────────────────────────────

    public async Task<ProcurementInboxMessage?> GetInboxMessageByIdempotencyKeyAsync(
        Guid tenantId, string messageType, string idempotencyKey, CancellationToken ct = default)
        => await _db.InboxMessages
            .FirstOrDefaultAsync(m =>
                m.TenantId == tenantId &&
                m.MessageType == messageType &&
                m.IdempotencyKey == idempotencyKey, ct).ConfigureAwait(false);

    public async Task AddInboxMessageAsync(ProcurementInboxMessage message, CancellationToken ct = default)
        => await _db.InboxMessages.AddAsync(message, ct).ConfigureAwait(false);

    // ── Audit Log ─────────────────────────────────────────────────────────────

    public async Task AddAuditLogAsync(ProcurementAuditLog entry, CancellationToken ct = default)
        => await _db.AuditLogs.AddAsync(entry, ct).ConfigureAwait(false);

    // ── RequisitionNumber ─────────────────────────────────────────────────────

    public async Task<string> GenerateRequisitionNumberAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational())
        {
            // In-memory fallback for tests
            return $"PR-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString()[..5].ToUpperInvariant()}";
        }

        // BE-P-09: advisory-lock-safe fn call inside the current transaction
        var result = await _db.Database
            .SqlQueryRaw<string>(
                "SELECT spaceos_procurement.fn_next_requisition_number({0}, {1}) AS \"Value\"",
                tenantId.ToString(), DateTime.UtcNow.Year)
            .FirstAsync(ct).ConfigureAwait(false);

        return result;
    }

    // ── Delivery InventorySyncStatus ─────────────────────────────────────────

    public async Task UpdateDeliveryInventorySyncStatusAsync(Guid deliveryId, string status, CancellationToken ct = default)
    {
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE spaceos_procurement.\"Deliveries\" SET \"InventorySyncStatus\" = {0} WHERE \"Id\" = {1}",
                status, deliveryId).ConfigureAwait(false);
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct).ConfigureAwait(false);
}
