using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetRequisitionById;

public sealed class GetRequisitionByIdQueryHandler
    : IRequestHandler<GetRequisitionByIdQuery, Result<RequisitionDto>>
{
    private readonly IProcurementV2Repository _repository;

    public GetRequisitionByIdQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<RequisitionDto>> Handle(GetRequisitionByIdQuery request, CancellationToken ct)
    {
        var r = await _repository.GetRequisitionByIdAsync(request.RequisitionId, ct).ConfigureAwait(false);
        if (r is null)
            return Result<RequisitionDto>.NotFound($"Requisition {request.RequisitionId} not found.");
        if (r.TenantId != request.TenantId)
            return Result<RequisitionDto>.NotFound();

        var dto = new RequisitionDto(
            r.Id, r.TenantId, r.RequisitionNumber, r.Source.ToString(), r.Status.ToString(),
            r.RequestedBy, r.ApprovedBy, r.ApprovedAt, r.RejectedReason,
            r.ConvertedPurchaseOrderId, r.Notes, r.CreatedAt,
            r.Lines.Select(l => new RequisitionLineDto(l.Id, l.MaterialCode, l.Quantity,
                l.EstimatedUnitPrice, l.PreferredSupplierId, l.Notes)).ToList());

        return Result<RequisitionDto>.Success(dto);
    }
}
