using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;

public sealed class ReceiveInvoiceCommandHandler
    : IRequestHandler<ReceiveInvoiceCommand, Result<Guid>>
{
    private readonly IProcurementV2Repository _repository;

    public ReceiveInvoiceCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(ReceiveInvoiceCommand request, CancellationToken ct)
    {
        // OPEN-06: check for duplicate invoice number
        var normalizedNumber = request.SupplierInvoiceNumber.Trim().ToUpperInvariant();
        var exists = await _repository.InvoiceNumberExistsAsync(
            request.TenantId, request.SupplierId, normalizedNumber, ct).ConfigureAwait(false);
        if (exists)
            return Result<Guid>.Conflict($"Invoice {normalizedNumber} already exists for this supplier.");

        var lines = request.Lines
            .Select(l => (l.MaterialCode, l.PurchaseOrderLineId, l.Quantity, l.UnitPrice, l.LineNetAmount, l.LineVatAmount))
            .ToList();

        var result = SupplierInvoice.Receive(
            request.TenantId,
            request.SupplierId,
            request.PurchaseOrderId,
            normalizedNumber,
            request.InvoiceDate,
            request.DueDate,
            request.Currency,
            request.RecordedBy,
            lines);

        if (!result.IsSuccess)
            return Result<Guid>.Invalid(result.ValidationErrors.ToArray());

        var invoice = result.Value;

        // BE-P-01: audit in same transaction
        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.RecordedBy,
            action: "InvoiceReceived",
            aggregateType: "SupplierInvoice",
            aggregateId: invoice.Id);

        await _repository.AddInvoiceAsync(invoice, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(invoice.Id);
    }
}
