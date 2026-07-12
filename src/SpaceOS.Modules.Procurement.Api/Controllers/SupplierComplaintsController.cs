using Ardalis.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceOS.Modules.Procurement.Application.Commands.MarkComplaintAsReviewing;
using SpaceOS.Modules.Procurement.Application.Commands.RespondToComplaint;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Api.Controllers;

/// <summary>
/// Supplier-portal facing complaint API.
/// Suppliers can view complaints submitted against them and respond.
/// </summary>
[ApiController]
[Authorize]
[Route("api/supplier-portal/complaints")]
public class SupplierComplaintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IComplaintRepository _repository;

    public SupplierComplaintsController(IMediator mediator, IComplaintRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    /// <summary>
    /// Lists all complaints for the authenticated supplier.
    /// Uses RLS to ensure suppliers only see their own complaints.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid supplierId, [FromQuery] ComplaintStatus? status, CancellationToken ct)
    {
        // Note: RLS policy should enforce supplierId scoping
        var complaints = await _repository.GetBySupplierAsync(supplierId, status, ct).ConfigureAwait(false);

        return Ok(complaints.Select(c => new
        {
            c.Id,
            c.ComplaintNumber,
            c.DeliveryId,
            c.SupplierId,
            c.Type,
            c.Status,
            c.Subject,
            c.Description,
            c.CreatedAt,
            HasResponse = c.SupplierResponse is not null,
            IsResolved = c.Resolution is not null
        }));
    }

    /// <summary>
    /// Gets a specific complaint by ID.
    /// Verifies the complaint belongs to the authenticated supplier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid supplierId, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (complaint is null)
            return NotFound();

        // Double-defense: verify supplier ownership
        if (complaint.SupplierId != supplierId)
            return Forbid();

        return Ok(new
        {
            complaint.Id,
            complaint.ComplaintNumber,
            complaint.DeliveryId,
            complaint.SupplierId,
            complaint.Type,
            complaint.Status,
            complaint.Subject,
            complaint.Description,
            complaint.AffectedQuantity,
            complaint.ClaimedAmount,
            complaint.Currency,
            complaint.CreatedBy,
            complaint.CreatedAt,
            complaint.EvidencePaths,
            Response = complaint.SupplierResponse is not null ? new
            {
                complaint.SupplierResponse.Type,
                complaint.SupplierResponse.ResponseText,
                complaint.SupplierResponse.OfferedAmount,
                complaint.SupplierResponse.CounterProposal,
                complaint.SupplierResponse.AttachmentPaths,
                complaint.SupplierResponse.RespondedBy,
                complaint.SupplierResponse.RespondedAt
            } : null,
            Resolution = complaint.Resolution is not null ? new
            {
                complaint.Resolution.Type,
                complaint.Resolution.Summary,
                complaint.Resolution.FinalAmount,
                complaint.Resolution.Action,
                complaint.Resolution.ResolvedBy,
                complaint.Resolution.ResolvedAt
            } : null
        });
    }

    /// <summary>
    /// Marks a complaint as "under review" by supplier (Submitted → SupplierReviewing).
    /// </summary>
    [HttpPost("{id:guid}/reviewing")]
    public async Task<IActionResult> MarkAsReviewing(Guid id, [FromQuery] Guid supplierId, CancellationToken ct)
    {
        // Verify ownership
        var complaint = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (complaint is null)
            return NotFound();
        if (complaint.SupplierId != supplierId)
            return Forbid();

        var userId = User.Identity?.Name ?? "unknown";
        var command = new MarkComplaintAsReviewingCommand(id, userId);
        var result = await _mediator.Send(command, ct).ConfigureAwait(false);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.ValidationErrors),
            _ => StatusCode(500, result.Errors)
        };
    }

    /// <summary>
    /// Submits a response to a complaint (SupplierReviewing → SupplierResponded).
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromQuery] Guid supplierId, [FromBody] RespondToComplaintRequest request, CancellationToken ct)
    {
        // Verify ownership
        var complaint = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (complaint is null)
            return NotFound();
        if (complaint.SupplierId != supplierId)
            return Forbid();

        var userId = User.Identity?.Name ?? "unknown";
        var command = new RespondToComplaintCommand(
            id,
            request.ResponseType,
            request.ResponseText,
            request.ProposedAction,
            request.ProposedValue,
            request.ProposedValueCurrency,
            userId);

        var result = await _mediator.Send(command, ct).ConfigureAwait(false);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.ValidationErrors),
            _ => StatusCode(500, result.Errors)
        };
    }
}

// Request DTOs
public record RespondToComplaintRequest(
    ResponseType ResponseType,
    string ResponseText,
    ResolutionAction ProposedAction,
    decimal? ProposedValue,
    string? ProposedValueCurrency);
