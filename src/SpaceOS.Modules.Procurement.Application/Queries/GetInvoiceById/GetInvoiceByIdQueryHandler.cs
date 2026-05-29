using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdQueryHandler
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IProcurementV2Repository _repository;

    public GetInvoiceByIdQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var i = await _repository.GetInvoiceByIdAsync(request.InvoiceId, ct).ConfigureAwait(false);
        if (i is null || i.TenantId != request.TenantId)
            return Result<InvoiceDto>.NotFound($"Invoice {request.InvoiceId} not found.");

        var dto = new InvoiceDto(
            i.Id, i.TenantId, i.SupplierId, i.PurchaseOrderId, i.SupplierInvoiceNumber,
            i.InvoiceDate, i.DueDate, i.Currency, i.Status.ToString(),
            i.TotalNetAmount, i.TotalVatAmount, i.TotalGrossAmount,
            i.LatestMatchId, i.RecordedBy, i.VarianceApprovedBy, i.DisputeReason, i.CreatedAt,
            i.Lines.Select(l => new InvoiceLineDto(l.Id, l.MaterialCode, l.PurchaseOrderLineId,
                l.Quantity, l.UnitPrice, l.LineNetAmount, l.LineVatAmount, l.LineGrossAmount)).ToList());

        return Result<InvoiceDto>.Success(dto);
    }
}
