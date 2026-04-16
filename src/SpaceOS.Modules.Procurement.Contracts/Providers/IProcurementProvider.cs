using SpaceOS.Modules.Procurement.Contracts.Dtos;

namespace SpaceOS.Modules.Procurement.Contracts.Providers;

/// <summary>
/// Contract for creating purchase orders, querying supplier prices, and recording deliveries.
/// Implementations live in the Procurement module — callers depend only on this interface.
/// </summary>
public interface IProcurementProvider
{
    /// <summary>Creates a new purchase order and returns its identifier.</summary>
    Task<Guid> CreatePurchaseOrderAsync(PurchaseOrderDto order, CancellationToken ct = default);

    /// <summary>Returns the current status of an existing purchase order.</summary>
    Task<PurchaseOrderDto> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Returns available supplier prices for the given material type.</summary>
    Task<IReadOnlyList<SupplierPriceDto>> GetSupplierPricesAsync(string materialType, CancellationToken ct = default);

    /// <summary>Records a delivery received against an existing purchase order.</summary>
    Task RecordDeliveryAsync(DeliveryDto delivery, CancellationToken ct = default);
}
