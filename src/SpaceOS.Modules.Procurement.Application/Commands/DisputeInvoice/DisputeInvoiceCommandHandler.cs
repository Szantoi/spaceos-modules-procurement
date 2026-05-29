using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.DisputeInvoice;

public sealed class DisputeInvoiceCommandHandler
    : IRequestHandler<DisputeInvoiceCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public DisputeInvoiceCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(DisputeInvoiceCommand request, CancellationToken ct)
    {
        if (!request.UserRoles.Contains("procurement.approver", StringComparer.OrdinalIgnoreCase))
        {
            var forbidAudit = ProcurementAuditLog.Create(
                request.TenantId, request.Actor, "ForbiddenAttempt", "SupplierInvoice", request.InvoiceId);
            await _repository.AddAuditLogAsync(forbidAudit, ct).ConfigureAwait(false);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Forbidden();
        }

        var invoice = await _repository.GetInvoiceByIdAsync(request.InvoiceId, ct).ConfigureAwait(false);
        if (invoice is null)
            return Result.NotFound($"Invoice {request.InvoiceId} not found.");
        if (invoice.TenantId != request.TenantId)
            return Result.Forbidden();

        var disputeResult = invoice.Dispute(request.Reason);
        if (!disputeResult.IsSuccess)
            return disputeResult;

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Actor,
            action: "InvoiceDisputed",
            aggregateType: "SupplierInvoice",
            aggregateId: invoice.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }
}
