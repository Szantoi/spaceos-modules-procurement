using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.RejectPurchaseRequisition;

public sealed class RejectPurchaseRequisitionCommandHandler
    : IRequestHandler<RejectPurchaseRequisitionCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public RejectPurchaseRequisitionCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(RejectPurchaseRequisitionCommand request, CancellationToken ct)
    {
        var requisition = await _repository.GetRequisitionByIdAsync(request.RequisitionId, ct).ConfigureAwait(false);
        if (requisition is null)
            return Result.NotFound($"Requisition {request.RequisitionId} not found.");

        if (requisition.TenantId != request.TenantId)
            return Result.Forbidden();

        var rejectResult = requisition.Reject(request.Reason);
        if (!rejectResult.IsSuccess)
            return rejectResult;

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Actor,
            action: "RequisitionRejected",
            aggregateType: "PurchaseRequisition",
            aggregateId: requisition.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }
}
