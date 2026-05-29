using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetInvoices;

public sealed class GetInvoicesQueryHandler
    : IRequestHandler<GetInvoicesQuery, Result<IReadOnlyList<InvoiceDto>>>
{
    private readonly IProcurementV2Repository _repository;

    public GetInvoicesQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<InvoiceDto>>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var invoices = await _repository.GetInvoicesByTenantAsync(request.TenantId, ct).ConfigureAwait(false);
        var dtos = invoices.Select(MapToDto).ToList();
        return Result<IReadOnlyList<InvoiceDto>>.Success(dtos);
    }

    private static InvoiceDto MapToDto(Domain.Aggregates.SupplierInvoice i) => new(
        i.Id, i.TenantId, i.SupplierId, i.PurchaseOrderId, i.SupplierInvoiceNumber,
        i.InvoiceDate, i.DueDate, i.Currency, i.Status.ToString(),
        i.TotalNetAmount, i.TotalVatAmount, i.TotalGrossAmount,
        i.LatestMatchId, i.RecordedBy, i.VarianceApprovedBy, i.DisputeReason, i.CreatedAt,
        i.Lines.Select(l => new InvoiceLineDto(l.Id, l.MaterialCode, l.PurchaseOrderLineId,
            l.Quantity, l.UnitPrice, l.LineNetAmount, l.LineVatAmount, l.LineGrossAmount)).ToList());
}
