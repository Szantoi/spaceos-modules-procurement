using MediatR;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Queries.GetBestPrice;
using SpaceOS.Modules.Procurement.Application.Queries.GetInvoiceById;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Application.Queries.GetRequisitionById;
using SpaceOS.Modules.Procurement.Application.Queries.GetRequisitions;
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

    // ── v2 additions (Track H) ────────────────────────────────────────────────

    public async Task<PurchaseRequisitionDto?> GetRequisitionByIdAsync(Guid tenantId, Guid requisitionId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRequisitionByIdQuery(tenantId, requisitionId), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return null;
        var r = result.Value;
        return new PurchaseRequisitionDto(
            r.Id, r.TenantId, r.RequisitionNumber, r.Source, null, r.Status,
            r.RequestedBy, r.ApprovedBy, r.ApprovedAt, r.RejectedReason,
            r.ConvertedPurchaseOrderId, r.Notes, r.CreatedAt,
            r.Lines.Select(l => new PurchaseRequisitionLineDto(l.Id, l.MaterialCode, l.Quantity, l.EstimatedUnitPrice, l.PreferredSupplierId)).ToList());
    }

    public async Task<IReadOnlyList<PurchaseRequisitionSummaryDto>> GetRequisitionsByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRequisitionsQuery(tenantId), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return Array.Empty<PurchaseRequisitionSummaryDto>();
        return result.Value.Select(r => new PurchaseRequisitionSummaryDto(
            r.Id, r.TenantId, r.RequisitionNumber, r.Source, r.Status, r.RequestedBy, r.CreatedAt)).ToList();
    }

    public async Task<SupplierInvoiceDto?> GetInvoiceByIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetInvoiceByIdQuery(tenantId, invoiceId), ct).ConfigureAwait(false);
        if (!result.IsSuccess) return null;
        var i = result.Value;
        return new SupplierInvoiceDto(
            i.Id, i.TenantId, i.SupplierId, i.PurchaseOrderId, i.SupplierInvoiceNumber,
            i.InvoiceDate, i.Currency, i.Status, i.TotalNetAmount, i.TotalGrossAmount);
    }

    public async Task<PriceListEntryDto?> GetBestPriceAsync(Guid tenantId, string materialCode, int quantity, string currency, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBestPriceQuery(tenantId, materialCode, quantity, currency), ct).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null) return null;
        var e = result.Value;
        return new PriceListEntryDto(e.Id, e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity);
    }
}
