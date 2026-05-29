using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoice;

public sealed class ApproveInvoiceCommandHandler
    : IRequestHandler<ApproveInvoiceCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public ApproveInvoiceCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(ApproveInvoiceCommand request, CancellationToken ct)
    {
        if (!request.UserRoles.Contains("procurement.approver", StringComparer.OrdinalIgnoreCase))
        {
            await WriteAuditAsync(request.TenantId, request.Approver, "ForbiddenAttempt", request.InvoiceId, ct).ConfigureAwait(false);
            return Result.Forbidden();
        }

        var invoice = await _repository.GetInvoiceByIdAsync(request.InvoiceId, ct).ConfigureAwait(false);
        if (invoice is null)
            return Result.NotFound($"Invoice {request.InvoiceId} not found.");
        if (invoice.TenantId != request.TenantId)
            return Result.Forbidden();

        var approveResult = invoice.Approve(request.Approver);
        if (!approveResult.IsSuccess)
            return approveResult;

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Approver,
            action: "InvoiceApproved",
            aggregateType: "SupplierInvoice",
            aggregateId: invoice.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task WriteAuditAsync(Guid tenantId, string actor, string action, Guid aggregateId, CancellationToken ct)
    {
        var audit = ProcurementAuditLog.Create(tenantId, actor, action, "SupplierInvoice", aggregateId);
        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
