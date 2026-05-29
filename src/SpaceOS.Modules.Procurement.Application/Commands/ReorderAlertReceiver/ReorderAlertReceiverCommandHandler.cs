using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ReorderAlertReceiver;

/// <summary>
/// Handles inbound reorder alerts from the Inventory worker.
/// BE-P-01: single DB transaction — inbox INSERT + requisition CREATE + audit in one SaveChanges.
/// Idempotency: duplicate idempotency key returns existing resultRef (200).
/// </summary>
public sealed class ReorderAlertReceiverCommandHandler
    : IRequestHandler<ReorderAlertReceiverCommand, ReorderAlertReceiverResult>
{
    private const string MessageType = "ReorderAlertReceived";
    private const string RequestedBy = "worker:reorder-alert";

    private readonly IProcurementV2Repository _repository;

    public ReorderAlertReceiverCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<ReorderAlertReceiverResult> Handle(ReorderAlertReceiverCommand request, CancellationToken ct)
    {
        var idempotencyKey = BuildIdempotencyKey(request);

        // Check for existing inbox message (idempotency)
        var existing = await _repository
            .GetInboxMessageByIdempotencyKeyAsync(request.TenantId, MessageType, idempotencyKey, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Duplicate: return the existing requisitionId
            var existingReqId = existing.ResultRef ?? Guid.Empty;
            return new ReorderAlertReceiverResult(existingReqId, IsDuplicate: true);
        }

        // Generate requisition number
        var requisitionNumber = await _repository
            .GenerateRequisitionNumberAsync(request.TenantId, ct)
            .ConfigureAwait(false);

        var lines = new[]
        {
            (
                MaterialCode: request.MaterialCode,
                Quantity: (int)Math.Max(1, Math.Ceiling(request.SuggestedQuantity)),
                EstimatedUnitPrice: (decimal?)null,
                PreferredSupplierId: request.PreferredSupplierId,
                Notes: (string?)$"ReorderAlert: stock={request.CurrentStock} reorderPoint={request.ReorderPoint} unit={request.UnitOfMeasure}"
            )
        };

        var createResult = PurchaseRequisition.Create(
            request.TenantId,
            requisitionNumber,
            RequisitionSource.ReorderAlert,
            sourceReference: null,
            requestedBy: RequestedBy,
            lines,
            notes: $"Auto-generated from reorder alert for {request.MaterialCode} at {request.AlertedAt:O}");

        if (!createResult.IsSuccess)
            throw new InvalidOperationException(
                $"Failed to create requisition from reorder alert: {string.Join("; ", createResult.ValidationErrors.Select(e => e.ErrorMessage))}");

        var requisition = createResult.Value;

        // Build inbox message for idempotency (inserted before requisition to be safe)
        var inbox = ProcurementInboxMessage.Create(request.TenantId, MessageType, idempotencyKey);

        // BE-P-01: audit in same transaction
        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: RequestedBy,
            action: "ReorderAlertReceived",
            aggregateType: "PurchaseRequisition",
            aggregateId: requisition.Id,
            afterJson: $"{{\"materialCode\":\"{request.MaterialCode}\",\"alertedAt\":\"{request.AlertedAt:O}\"}}");

        await _repository.AddInboxMessageAsync(inbox, ct).ConfigureAwait(false);
        await _repository.AddRequisitionAsync(requisition, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);

        // One SaveChanges — BE-P-01 UoW
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        // Mark inbox as processed (second SaveChanges — still within the request scope)
        inbox.MarkProcessed(requisition.Id);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return new ReorderAlertReceiverResult(requisition.Id, IsDuplicate: false);
    }

    private static string BuildIdempotencyKey(ReorderAlertReceiverCommand request)
        => $"{request.MaterialCode}:{request.AlertedAt:O}";
}
