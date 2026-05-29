using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoiceWithVariance;

public sealed class ApproveInvoiceWithVarianceCommandHandler
    : IRequestHandler<ApproveInvoiceWithVarianceCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public ApproveInvoiceWithVarianceCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(ApproveInvoiceWithVarianceCommand request, CancellationToken ct)
    {
        if (!request.UserRoles.Contains("procurement.approver", StringComparer.OrdinalIgnoreCase))
        {
            var forbidAudit = ProcurementAuditLog.Create(
                request.TenantId, request.Approver, "ForbiddenAttempt", "SupplierInvoice", request.InvoiceId);
            await _repository.AddAuditLogAsync(forbidAudit, ct).ConfigureAwait(false);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Forbidden();
        }

        var invoice = await _repository.GetInvoiceByIdAsync(request.InvoiceId, ct).ConfigureAwait(false);
        if (invoice is null)
            return Result.NotFound($"Invoice {request.InvoiceId} not found.");
        if (invoice.TenantId != request.TenantId)
            return Result.Forbidden();

        var approveResult = invoice.ApproveWithVariance(request.Approver);
        if (!approveResult.IsSuccess)
        {
            if (approveResult.Status == Ardalis.Result.ResultStatus.Forbidden)
            {
                var sodAudit = ProcurementAuditLog.Create(
                    request.TenantId,
                    actor: request.Approver,
                    action: "ForbiddenAttempt",
                    aggregateType: "SupplierInvoice",
                    aggregateId: invoice.Id,
                    afterJson: "{\"reason\":\"SoD violation: varianceApprover == recordedBy\"}");
                await _repository.AddAuditLogAsync(sodAudit, ct).ConfigureAwait(false);
                await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            return approveResult;
        }

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Approver,
            action: "InvoiceApprovedWithVariance",
            aggregateType: "SupplierInvoice",
            aggregateId: invoice.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }
}
