using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Domain.Interfaces;

public interface IProcurementRepository
{
    Task<Supplier?> GetSupplierByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetActiveSuppliersByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddSupplierAsync(Supplier supplier, CancellationToken ct = default);

    Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrder>> GetOrdersByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task AddDeliveryAsync(Delivery delivery, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
