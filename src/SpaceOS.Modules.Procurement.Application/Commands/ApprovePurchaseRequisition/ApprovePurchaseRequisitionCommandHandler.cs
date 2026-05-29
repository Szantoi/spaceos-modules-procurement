using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApprovePurchaseRequisition;

public sealed class ApprovePurchaseRequisitionCommandHandler
    : IRequestHandler<ApprovePurchaseRequisitionCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public ApprovePurchaseRequisitionCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(ApprovePurchaseRequisitionCommand request, CancellationToken ct)
    {
        // SEC-P-02: RBAC check
        if (!request.UserRoles.Contains("procurement.approver", StringComparer.OrdinalIgnoreCase))
        {
            var forbiddenAudit = ProcurementAuditLog.Create(
                request.TenantId,
                actor: request.Approver,
                action: "ForbiddenAttempt",
                aggregateType: "PurchaseRequisition",
                aggregateId: request.RequisitionId);
            await _repository.AddAuditLogAsync(forbiddenAudit, ct).ConfigureAwait(false);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Forbidden();
        }

        var requisition = await _repository.GetRequisitionByIdAsync(request.RequisitionId, ct).ConfigureAwait(false);
        if (requisition is null)
            return Result.NotFound($"Requisition {request.RequisitionId} not found.");

        // EnsureSameTenant
        if (requisition.TenantId != request.TenantId)
            return Result.Forbidden();

        var approveResult = requisition.Approve(request.Approver);
        if (!approveResult.IsSuccess)
        {
            if (approveResult.Status == Ardalis.Result.ResultStatus.Forbidden)
            {
                // SoD violation — audit it (SEC-P-05)
                var sodAudit = ProcurementAuditLog.Create(
                    request.TenantId,
                    actor: request.Approver,
                    action: "ForbiddenAttempt",
                    aggregateType: "PurchaseRequisition",
                    aggregateId: request.RequisitionId,
                    afterJson: $"{{\"reason\":\"SoD violation: approver == requestedBy\"}}");
                await _repository.AddAuditLogAsync(sodAudit, ct).ConfigureAwait(false);
                await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            return approveResult;
        }

        // BE-P-01: audit in same transaction
        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Approver,
            action: "RequisitionApproved",
            aggregateType: "PurchaseRequisition",
            aggregateId: requisition.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }
}
