using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;

public sealed class CreatePurchaseRequisitionCommandHandler
    : IRequestHandler<CreatePurchaseRequisitionCommand, Result<Guid>>
{
    private readonly IProcurementV2Repository _repository;

    public CreatePurchaseRequisitionCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseRequisitionCommand request, CancellationToken ct)
    {
        // BE-P-09: generate requisition number inside the transaction, after validation
        var requisitionNumber = await _repository.GenerateRequisitionNumberAsync(request.TenantId, ct).ConfigureAwait(false);

        var lines = request.Lines
            .Select(l => (l.MaterialCode, l.Quantity, l.EstimatedUnitPrice, l.PreferredSupplierId, l.Notes))
            .ToList();

        var result = PurchaseRequisition.Create(
            request.TenantId,
            requisitionNumber,
            RequisitionSource.Manual,
            sourceReference: null,
            request.RequestedBy,
            lines,
            request.Notes);

        if (!result.IsSuccess)
            return Result<Guid>.Invalid(result.ValidationErrors.ToArray());

        var requisition = result.Value;

        // BE-P-01: audit log in same transaction
        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.RequestedBy,
            action: "RequisitionCreated",
            aggregateType: "PurchaseRequisition",
            aggregateId: requisition.Id);

        await _repository.AddRequisitionAsync(requisition, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(requisition.Id);
    }
}
