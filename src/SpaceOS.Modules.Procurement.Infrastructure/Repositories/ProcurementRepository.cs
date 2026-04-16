using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Repositories;

public class ProcurementRepository : IProcurementRepository
{
    private readonly ProcurementDbContext _db;

    public ProcurementRepository(ProcurementDbContext db)
    {
        _db = db;
    }

    public async Task<Supplier?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Supplier>> GetActiveSuppliersByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Suppliers.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task AddSupplierAsync(Supplier supplier, CancellationToken ct = default)
        => await _db.Suppliers.AddAsync(supplier, ct).ConfigureAwait(false);

    public async Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PurchaseOrders
            .FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);

    public async Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken ct = default)
        => await _db.PurchaseOrders.AddAsync(order, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PurchaseOrder>> GetOrdersByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.PurchaseOrders.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task AddDeliveryAsync(Delivery delivery, CancellationToken ct = default)
        => await _db.Deliveries.AddAsync(delivery, ct).ConfigureAwait(false);

    public async Task<TenantDeletedCounts> DeleteAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var deliveries = await _db.Deliveries
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);
        _db.Deliveries.RemoveRange(deliveries);

        var orders = await _db.PurchaseOrders
            .Where(o => o.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);
        _db.PurchaseOrders.RemoveRange(orders);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new TenantDeletedCounts(orders.Count, deliveries.Count);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct).ConfigureAwait(false);
}
