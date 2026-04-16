using MediatR;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Application.Queries.GetSupplierPrices;
using SpaceOS.Modules.Procurement.Contracts.Dtos;
using SpaceOS.Modules.Procurement.Contracts.Providers;

namespace SpaceOS.Modules.Procurement.Infrastructure.Adapters;

public class ProcurementProviderAdapter : IProcurementProvider
{
    private readonly IMediator _mediator;
    private readonly IProcurementTenantAccessor _tenantAccessor;

    public ProcurementProviderAdapter(IMediator mediator, IProcurementTenantAccessor tenantAccessor)
    {
        _mediator = mediator;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<Guid> CreatePurchaseOrderAsync(PurchaseOrderDto order, CancellationToken ct = default)
    {
        var command = new CreatePurchaseOrderCommand(
            order.TenantId,
            order.SupplierId,
            order.MaterialType,
            order.Quantity,
            0m, // unit price not in PurchaseOrderDto — use 0 as placeholder
            "HUF",
            order.ExpectedDelivery);

        var result = await _mediator.Send(command, ct).ConfigureAwait(false);
        if (!result.IsSuccess) throw new InvalidOperationException(string.Join(", ", result.Errors));
        return result.Value;
    }

    public async Task<PurchaseOrderDto> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrderStatusQuery(orderId), ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return new PurchaseOrderDto(orderId, Guid.Empty, Guid.Empty, "Unknown", 0m, "NotFound", null);

        return new PurchaseOrderDto(
            result.Value.Id,
            Guid.Empty,
            Guid.Empty,
            result.Value.MaterialType,
            result.Value.Quantity,
            result.Value.Status,
            result.Value.ExpectedDelivery);
    }

    public async Task<IReadOnlyList<SupplierPriceDto>> GetSupplierPricesAsync(string materialType, CancellationToken ct = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        var result = await _mediator.Send(new GetSupplierPricesQuery(tenantId, materialType), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return Array.Empty<SupplierPriceDto>();

        return result.Value.Select(p => new SupplierPriceDto(
            p.SupplierId,
            p.SupplierName,
            materialType,
            0m,
            p.UnitPrice,
            "HUF",
            DateTime.UtcNow.AddDays(30))).ToList();
    }

    public async Task RecordDeliveryAsync(DeliveryDto delivery, CancellationToken ct = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        var command = new RecordDeliveryCommand(
            tenantId,
            delivery.PurchaseOrderId,
            delivery.PanelCount,
            delivery.Notes,
            "system");

        var result = await _mediator.Send(command, ct).ConfigureAwait(false);
        if (!result.IsSuccess) throw new InvalidOperationException(string.Join(", ", result.Errors));
    }
}
