using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Subcontract order aggregate root.
/// Represents work delegated to an external supplier (bérmunka).
/// </summary>
public class SubcontractOrder : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public SubcontractStatus Status { get; private set; }

    public string WorkDescription { get; private set; } = string.Empty;
    public decimal EstimatedCost { get; private set; }
    public string Currency { get; private set; } = "HUF";
    public DateTime Deadline { get; private set; }

    public string? RejectionReason { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private SubcontractOrder() { }

    /// <summary>
    /// Creates a new subcontract order in Pending status.
    /// </summary>
    public static Result<SubcontractOrder> Create(
        Guid tenantId,
        Guid supplierId,
        string workDescription,
        decimal estimatedCost,
        string? currency,
        DateTime deadline,
        string createdBy)
    {
        // Validation
        if (tenantId == Guid.Empty)
            return Result.Invalid(new ValidationError("TenantId required."));
        if (supplierId == Guid.Empty)
            return Result.Invalid(new ValidationError("SupplierId required."));

        if (string.IsNullOrWhiteSpace(workDescription))
            return Result.Invalid(new ValidationError("WorkDescription required."));
        if (workDescription.Length > 5000)
            return Result.Invalid(new ValidationError("WorkDescription max 5000 characters."));

        if (estimatedCost <= 0)
            return Result.Invalid(new ValidationError("EstimatedCost must be positive."));

        if (deadline <= DateTime.UtcNow)
            return Result.Invalid(new ValidationError("Deadline must be in the future."));

        if (string.IsNullOrWhiteSpace(createdBy))
            return Result.Invalid(new ValidationError("CreatedBy required."));

        var currencyCode = string.IsNullOrWhiteSpace(currency) ? "HUF" : currency.ToUpperInvariant();
        if (currencyCode.Length != 3)
            return Result.Invalid(new ValidationError("Currency must be 3-char ISO code."));

        var order = new SubcontractOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            OrderNumber = string.Empty, // Will be set by repository
            Status = SubcontractStatus.Pending,
            WorkDescription = workDescription,
            EstimatedCost = estimatedCost,
            Currency = currencyCode,
            Deadline = deadline,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        order.RaiseDomainEvent(new SubcontractOrderCreated(
            order.Id,
            order.TenantId,
            order.SupplierId,
            order.WorkDescription,
            order.Deadline
        ));

        return Result.Success(order);
    }

    /// <summary>
    /// Sets the order number (called by repository after DB insert).
    /// </summary>
    public void SetOrderNumber(string orderNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        if (!string.IsNullOrEmpty(OrderNumber))
            throw new InvalidOperationException("OrderNumber already set.");

        OrderNumber = orderNumber;
    }

    /// <summary>
    /// Partner accepts the subcontract order.
    /// Transition: Pending → Accepted
    /// </summary>
    public Result Accept()
    {
        if (Status != SubcontractStatus.Pending)
            return Result.Invalid(new ValidationError($"Cannot accept order in status {Status}."));

        Status = SubcontractStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SubcontractOrderAccepted(
            Id,
            TenantId,
            SupplierId
        ));

        return Result.Success();
    }

    /// <summary>
    /// Partner rejects the subcontract order.
    /// Transition: Pending → Rejected
    /// </summary>
    public Result Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Invalid(new ValidationError("Rejection reason required."));

        if (Status != SubcontractStatus.Pending)
            return Result.Invalid(new ValidationError($"Cannot reject order in status {Status}."));

        Status = SubcontractStatus.Rejected;
        RejectionReason = reason;

        RaiseDomainEvent(new SubcontractOrderRejected(
            Id,
            TenantId,
            SupplierId,
            reason
        ));

        return Result.Success();
    }

    /// <summary>
    /// Start work on the subcontract.
    /// Transition: Accepted → InProgress
    /// </summary>
    public Result StartWork()
    {
        if (Status != SubcontractStatus.Accepted)
            return Result.Invalid(new ValidationError($"Cannot start work in status {Status}."));

        Status = SubcontractStatus.InProgress;

        RaiseDomainEvent(new SubcontractOrderStarted(
            Id,
            TenantId,
            SupplierId
        ));

        return Result.Success();
    }

    /// <summary>
    /// Complete the subcontract work.
    /// Transition: InProgress → Completed
    /// </summary>
    public Result Complete()
    {
        if (Status != SubcontractStatus.InProgress)
            return Result.Invalid(new ValidationError($"Cannot complete order in status {Status}."));

        Status = SubcontractStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SubcontractOrderCompleted(
            Id,
            TenantId,
            SupplierId,
            CompletedAt.Value
        ));

        return Result.Success();
    }

    /// <summary>
    /// Cancel the subcontract order (tenant action).
    /// Transition: Pending | Accepted | InProgress → Cancelled
    /// </summary>
    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Invalid(new ValidationError("Cancellation reason required."));

        if (Status == SubcontractStatus.Completed || Status == SubcontractStatus.Cancelled || Status == SubcontractStatus.Rejected)
            return Result.Invalid(new ValidationError($"Cannot cancel order in status {Status}."));

        Status = SubcontractStatus.Cancelled;

        RaiseDomainEvent(new SubcontractOrderCancelled(
            Id,
            TenantId,
            SupplierId,
            reason
        ));

        return Result.Success();
    }
}
