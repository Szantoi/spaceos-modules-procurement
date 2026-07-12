using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Supplier complaint aggregate root.
/// Tracks quality/delivery/documentation issues and supplier response flow.
/// </summary>
public class SupplierComplaint : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ComplaintNumber { get; private set; } = string.Empty;

    // Relationships
    public Guid SupplierId { get; private set; }
    public Guid DeliveryId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }

    // Complaint content
    public ComplaintType Type { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal AffectedQuantity { get; private set; }
    public decimal? ClaimedAmount { get; private set; }
    public string Currency { get; private set; } = "HUF";

    // QA data (denormalized from Delivery)
    public QualityInspectionResult? QaResult { get; private set; }
    public List<string> EvidencePaths { get; private set; } = new();

    // FSM
    public ComplaintStatus Status { get; private set; }

    // Audit trail
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Owned entities
    public ComplaintResponse? SupplierResponse { get; private set; }
    public ComplaintResolution? Resolution { get; private set; }

    private SupplierComplaint() { }

    /// <summary>
    /// Creates a new supplier complaint in Draft status.
    /// </summary>
    public static Result<SupplierComplaint> Create(
        Guid tenantId,
        Guid supplierId,
        Guid deliveryId,
        Guid? purchaseOrderId,
        ComplaintType type,
        string subject,
        string description,
        decimal affectedQuantity,
        decimal? claimedAmount,
        string? currency,
        QualityInspectionResult? qaResult,
        List<string>? evidencePaths,
        string createdBy)
    {
        // Validation
        if (tenantId == Guid.Empty)
            return Result.Invalid(new ValidationError("TenantId required."));
        if (supplierId == Guid.Empty)
            return Result.Invalid(new ValidationError("SupplierId required."));
        if (deliveryId == Guid.Empty)
            return Result.Invalid(new ValidationError("DeliveryId required."));

        if (string.IsNullOrWhiteSpace(subject))
            return Result.Invalid(new ValidationError("Subject required."));
        if (subject.Length > 200)
            return Result.Invalid(new ValidationError("Subject max 200 characters."));

        if (string.IsNullOrWhiteSpace(description))
            return Result.Invalid(new ValidationError("Description required."));
        if (description.Length > 5000)
            return Result.Invalid(new ValidationError("Description max 5000 characters."));

        if (affectedQuantity <= 0)
            return Result.Invalid(new ValidationError("AffectedQuantity must be positive."));

        if (string.IsNullOrWhiteSpace(createdBy))
            return Result.Invalid(new ValidationError("CreatedBy required."));

        var currencyCode = string.IsNullOrWhiteSpace(currency) ? "HUF" : currency.ToUpperInvariant();
        if (currencyCode.Length != 3)
            return Result.Invalid(new ValidationError("Currency must be 3-char ISO code."));

        var complaint = new SupplierComplaint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ComplaintNumber = string.Empty, // Will be set by repository on insert
            SupplierId = supplierId,
            DeliveryId = deliveryId,
            PurchaseOrderId = purchaseOrderId,
            Type = type,
            Subject = subject,
            Description = description,
            AffectedQuantity = affectedQuantity,
            ClaimedAmount = claimedAmount,
            Currency = currencyCode,
            QaResult = qaResult,
            EvidencePaths = evidencePaths ?? new List<string>(),
            Status = ComplaintStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        complaint.RaiseDomainEvent(new SupplierComplaintCreated(
            complaint.Id,
            complaint.TenantId,
            complaint.SupplierId,
            complaint.DeliveryId,
            complaint.Type,
            complaint.Subject
        ));

        return Result.Success(complaint);
    }

    /// <summary>
    /// Sets the complaint number (called by repository after DB insert).
    /// </summary>
    public void SetComplaintNumber(string complaintNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(complaintNumber);
        if (!string.IsNullOrEmpty(ComplaintNumber))
            throw new InvalidOperationException("ComplaintNumber already set.");

        ComplaintNumber = complaintNumber;
    }

    /// <summary>
    /// Submits the complaint to the supplier.
    /// Transition: Draft → Submitted
    /// </summary>
    public Result Submit(string submittedBy)
    {
        if (string.IsNullOrWhiteSpace(submittedBy))
            return Result.Invalid(new ValidationError("SubmittedBy required."));

        if (Status != ComplaintStatus.Draft)
            return Result.Invalid(new ValidationError($"Cannot submit complaint in status {Status}."));

        Status = ComplaintStatus.Submitted;

        RaiseDomainEvent(new SupplierComplaintSubmitted(
            Id,
            TenantId,
            SupplierId,
            submittedBy
        ));

        return Result.Success();
    }

    /// <summary>
    /// Withdraws the complaint (tenant action).
    /// Transition: Submitted | SupplierReviewing | UnderReview → Withdrawn
    /// </summary>
    public Result Withdraw(string withdrawnBy, string reason)
    {
        if (string.IsNullOrWhiteSpace(withdrawnBy))
            return Result.Invalid(new ValidationError("WithdrawnBy required."));
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Invalid(new ValidationError("Reason required."));

        if (Status != ComplaintStatus.Submitted &&
            Status != ComplaintStatus.SupplierReviewing &&
            Status != ComplaintStatus.UnderReview)
            return Result.Invalid(new ValidationError($"Cannot withdraw complaint in status {Status}."));

        Status = ComplaintStatus.Withdrawn;

        RaiseDomainEvent(new SupplierComplaintWithdrawn(
            Id,
            TenantId,
            reason,
            withdrawnBy
        ));

        return Result.Success();
    }

    /// <summary>
    /// Marks complaint as under review by supplier.
    /// Transition: Submitted → SupplierReviewing
    /// </summary>
    public Result MarkAsReviewing(string reviewedBy)
    {
        if (string.IsNullOrWhiteSpace(reviewedBy))
            return Result.Invalid(new ValidationError("ReviewedBy required."));

        if (Status != ComplaintStatus.Submitted)
            return Result.Invalid(new ValidationError($"Cannot mark as reviewing in status {Status}."));

        Status = ComplaintStatus.SupplierReviewing;

        RaiseDomainEvent(new SupplierComplaintReviewing(
            Id,
            TenantId,
            SupplierId,
            reviewedBy
        ));

        return Result.Success();
    }

    /// <summary>
    /// Supplier responds to the complaint.
    /// Transition: SupplierReviewing → SupplierResponded
    /// </summary>
    public Result Respond(ComplaintResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (Status != ComplaintStatus.SupplierReviewing)
            return Result.Invalid(new ValidationError($"Cannot respond in status {Status}."));

        if (SupplierResponse is not null)
            return Result.Invalid(new ValidationError("Supplier has already responded."));

        SupplierResponse = response;
        Status = ComplaintStatus.SupplierResponded;

        RaiseDomainEvent(new SupplierComplaintResponded(
            Id,
            TenantId,
            SupplierId,
            response.Type,
            response.RespondedBy
        ));

        return Result.Success();
    }

    /// <summary>
    /// Tenant accepts supplier's response and moves to review.
    /// Transition: SupplierResponded → UnderReview
    /// </summary>
    public Result AcceptResponse(string acceptedBy)
    {
        if (string.IsNullOrWhiteSpace(acceptedBy))
            return Result.Invalid(new ValidationError("AcceptedBy required."));

        if (Status != ComplaintStatus.SupplierResponded)
            return Result.Invalid(new ValidationError($"Cannot accept response in status {Status}."));

        Status = ComplaintStatus.UnderReview;

        RaiseDomainEvent(new SupplierResponseAccepted(
            Id,
            TenantId,
            acceptedBy
        ));

        return Result.Success();
    }

    /// <summary>
    /// Resolves the complaint with final decision.
    /// Transition: UnderReview → Resolved | Escalated
    /// </summary>
    public Result Resolve(ComplaintResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        if (Status != ComplaintStatus.UnderReview)
            return Result.Invalid(new ValidationError($"Cannot resolve in status {Status}."));

        if (Resolution is not null)
            return Result.Invalid(new ValidationError("Complaint already resolved."));

        Resolution = resolution;

        // If rejected → escalated, else → resolved
        Status = resolution.Type == ResolutionType.Rejected
            ? ComplaintStatus.Escalated
            : ComplaintStatus.Resolved;

        RaiseDomainEvent(new SupplierComplaintResolved(
            Id,
            TenantId,
            resolution.Type,
            resolution.ResolvedBy
        ));

        return Result.Success();
    }
}
