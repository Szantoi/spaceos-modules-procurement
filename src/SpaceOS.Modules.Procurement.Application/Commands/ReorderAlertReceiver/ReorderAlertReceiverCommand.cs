using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ReorderAlertReceiver;

/// <summary>
/// Processes an inbound reorder alert from the Inventory worker.
/// Track E: idempotent — returns existing requisitionId on duplicate.
/// BE-P-01: inbox INSERT + requisition CREATE in one transaction.
/// </summary>
public sealed record ReorderAlertReceiverCommand(
    Guid TenantId,
    string MaterialCode,
    decimal CurrentStock,
    decimal ReorderPoint,
    decimal SuggestedQuantity,
    Guid? PreferredSupplierId,
    string UnitOfMeasure,
    DateTimeOffset AlertedAt) : IRequest<ReorderAlertReceiverResult>;

/// <summary>Result of processing a reorder alert.</summary>
/// <param name="RequisitionId">The created or existing requisition ID.</param>
/// <param name="IsDuplicate">True if the alert was already processed (idempotent reply).</param>
public sealed record ReorderAlertReceiverResult(Guid RequisitionId, bool IsDuplicate);
