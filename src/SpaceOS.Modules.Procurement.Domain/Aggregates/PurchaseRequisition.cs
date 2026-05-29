using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Purchase requisition aggregate root.
/// FSM: Draft → Approved → ConvertedToPO (terminal)
///      Draft | Approved → Rejected (terminal)
/// </summary>
public sealed class PurchaseRequisition : AggregateRoot
{
    private readonly List<PurchaseRequisitionLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string RequisitionNumber { get; private set; } = string.Empty;
    public RequisitionSource Source { get; private set; }
    public Guid? SourceReference { get; private set; }
    public RequisitionStatus Status { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? RejectedReason { get; private set; }
    public Guid? ConvertedPurchaseOrderId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Read-only view of requisition lines.</summary>
    public IReadOnlyList<PurchaseRequisitionLine> Lines => _lines.AsReadOnly();

    private PurchaseRequisition() { }

    /// <summary>
    /// Creates a new draft requisition. RequisitionNumber must be pre-generated (fn_next_requisition_number).
    /// </summary>
    public static Result<PurchaseRequisition> Create(
        Guid tenantId,
        string requisitionNumber,
        RequisitionSource source,
        Guid? sourceReference,
        string requestedBy,
        IReadOnlyList<(string MaterialCode, int Quantity, decimal? EstimatedUnitPrice, Guid? PreferredSupplierId, string? Notes)> lines,
        string? notes = null)
    {
        if (tenantId == Guid.Empty)
            return Result<PurchaseRequisition>.Invalid(new ValidationError("TenantId is required."));
        ArgumentException.ThrowIfNullOrWhiteSpace(requisitionNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);
        if (lines is null || lines.Count == 0)
            return Result<PurchaseRequisition>.Invalid(new ValidationError("At least one line is required."));
        if (notes?.Length > 2000)
            return Result<PurchaseRequisition>.Invalid(new ValidationError("Notes must be ≤2000 characters."));

        var requisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionNumber = requisitionNumber,
            Source = source,
            SourceReference = sourceReference,
            Status = RequisitionStatus.Draft,
            RequestedBy = requestedBy,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (mc, qty, eup, ps, ln) in lines)
        {
            var line = PurchaseRequisitionLine.Create(requisition.Id, tenantId, mc, qty, eup, ps, ln);
            requisition._lines.Add(line);
        }

        requisition.RaiseDomainEvent(new PurchaseRequisitionCreatedEvent(
            requisition.Id, tenantId, source, sourceReference));

        return Result<PurchaseRequisition>.Success(requisition);
    }

    /// <summary>
    /// Approves the requisition.
    /// Guard: Status must be Draft.
    /// SoD (SEC-P-03): approver != RequestedBy unless Source is ReorderAlert.
    /// </summary>
    public Result Approve(string approver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approver);

        if (Status != RequisitionStatus.Draft)
            return Result.Invalid(new ValidationError($"Cannot approve a requisition in status {Status}."));

        // SoD: human approver must differ from requester (worker:reorder-alert bypasses this)
        if (Source != RequisitionSource.ReorderAlert &&
            string.Equals(approver, RequestedBy, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Forbidden();
        }

        Status = RequisitionStatus.Approved;
        ApprovedBy = approver;
        ApprovedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PurchaseRequisitionApprovedEvent(Id, TenantId, approver));
        return Result.Success();
    }

    /// <summary>
    /// Rejects the requisition.
    /// Guard: Status must be Draft or Approved.
    /// </summary>
    public Result Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Invalid(new ValidationError("Rejection reason is required."));
        if (reason.Length > 2000)
            return Result.Invalid(new ValidationError("Rejection reason must be ≤2000 characters."));

        if (Status != RequisitionStatus.Draft && Status != RequisitionStatus.Approved)
            return Result.Invalid(new ValidationError($"Cannot reject a requisition in status {Status}."));

        Status = RequisitionStatus.Rejected;
        RejectedReason = reason;

        RaiseDomainEvent(new PurchaseRequisitionRejectedEvent(Id, TenantId, reason));
        return Result.Success();
    }

    /// <summary>
    /// Converts the approved requisition to a purchase order.
    /// Guard: Status must be Approved.
    /// </summary>
    public Result ConvertToPurchaseOrder(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty)
            return Result.Invalid(new ValidationError("PurchaseOrderId is required."));

        if (Status != RequisitionStatus.Approved)
            return Result.Invalid(new ValidationError($"Cannot convert a requisition in status {Status}."));

        Status = RequisitionStatus.ConvertedToPO;
        ConvertedPurchaseOrderId = purchaseOrderId;

        RaiseDomainEvent(new PurchaseRequisitionConvertedToPOEvent(Id, TenantId, purchaseOrderId));
        return Result.Success();
    }
}
