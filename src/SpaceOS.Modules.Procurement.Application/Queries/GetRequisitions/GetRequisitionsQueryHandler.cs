using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetRequisitions;

public sealed class GetRequisitionsQueryHandler
    : IRequestHandler<GetRequisitionsQuery, Result<IReadOnlyList<RequisitionDto>>>
{
    private readonly IProcurementV2Repository _repository;

    public GetRequisitionsQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<RequisitionDto>>> Handle(GetRequisitionsQuery request, CancellationToken ct)
    {
        var requisitions = await _repository.GetRequisitionsByTenantAsync(request.TenantId, ct).ConfigureAwait(false);
        var dtos = requisitions.Select(r => new RequisitionDto(
            r.Id, r.TenantId, r.RequisitionNumber, r.Source.ToString(), r.Status.ToString(),
            r.RequestedBy, r.ApprovedBy, r.ApprovedAt, r.RejectedReason,
            r.ConvertedPurchaseOrderId, r.Notes, r.CreatedAt,
            r.Lines.Select(l => new RequisitionLineDto(l.Id, l.MaterialCode, l.Quantity,
                l.EstimatedUnitPrice, l.PreferredSupplierId, l.Notes)).ToList()
        )).ToList();

        return Result<IReadOnlyList<RequisitionDto>>.Success(dtos);
    }
}
