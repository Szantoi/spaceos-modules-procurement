using Ardalis.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceOS.Modules.Procurement.Application.Commands.CreateComplaint;
using SpaceOS.Modules.Procurement.Application.Commands.SubmitComplaint;
using SpaceOS.Modules.Procurement.Application.Commands.WithdrawComplaint;
using SpaceOS.Modules.Procurement.Application.Commands.AcceptComplaintResponse;
using SpaceOS.Modules.Procurement.Application.Commands.ResolveComplaint;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Api.Controllers;

/// <summary>
/// Tenant-facing complaint management API.
/// </summary>
[ApiController]
[Authorize]
[Route("api/procurement/complaints")]
public class ComplaintsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IComplaintRepository _repository;

    public ComplaintsController(IMediator mediator, IComplaintRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    /// <summary>
    /// Creates a new supplier complaint (Draft status).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = User.Identity?.Name ?? "unknown";

        var command = new CreateComplaintCommand(
            tenantId,
            request.SupplierId,
            request.DeliveryId,
            request.PurchaseOrderId,
            request.Type,
            request.Subject,
            request.Description,
            request.AffectedQuantity,
            request.ClaimedAmount,
            request.Currency,
            request.EvidencePaths,
            userId);

        var result = await _mediator.Send(command, ct).ConfigureAwait(false);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(new { id = result.Value }),
            ResultStatus.Invalid => BadRequest(result.ValidationErrors),
            _ => StatusCode(500, result.Errors)
        };
    }

    /// <summary>
    /// Lists all complaints for the current tenant.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ComplaintStatus? status, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var complaints = await _repository.GetByTenantAsync(tenantId, status, null, ct).ConfigureAwait(false);

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
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (complaint is null)
            return NotFound();

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
    /// Submits a complaint (Draft → Submitted).
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        var command = new SubmitComplaintCommand(id, userId);
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
    /// Accepts the supplier's response (SupplierResponded → UnderReview).
    /// </summary>
    [HttpPost("{id:guid}/accept-response")]
    public async Task<IActionResult> AcceptResponse(Guid id, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        var command = new AcceptComplaintResponseCommand(id, userId);
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
    /// Resolves a complaint manually (UnderReview → Resolved/Escalated).
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveComplaintRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        var command = new ResolveComplaintCommand(
            id,
            request.ResolutionType,
            request.ResolutionAction,
            request.ResolutionValue,
            request.ResolutionValueCurrency,
            request.ResolutionNotes,
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

    /// <summary>
    /// Withdraws a complaint (Submitted/SupplierReviewing/UnderReview → Withdrawn).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawComplaintRequest request, CancellationToken ct)
    {
        var userId = User.Identity?.Name ?? "unknown";
        var command = new WithdrawComplaintCommand(id, userId, request.Reason);
        var result = await _mediator.Send(command, ct).ConfigureAwait(false);

        return result.Status switch
        {
            ResultStatus.Ok => Ok(),
            ResultStatus.NotFound => NotFound(),
            ResultStatus.Invalid => BadRequest(result.ValidationErrors),
            _ => StatusCode(500, result.Errors)
        };
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim is not null && Guid.TryParse(claim.Value, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }
}

// Request DTOs
public record CreateComplaintRequest(
    Guid SupplierId,
    Guid DeliveryId,
    Guid? PurchaseOrderId,
    ComplaintType Type,
    string Subject,
    string Description,
    decimal AffectedQuantity,
    decimal? ClaimedAmount,
    string? Currency,
    List<string>? EvidencePaths);

public record ResolveComplaintRequest(
    ResolutionType ResolutionType,
    ResolutionAction ResolutionAction,
    decimal? ResolutionValue,
    string? ResolutionValueCurrency,
    string? ResolutionNotes);

public record WithdrawComplaintRequest(string Reason);
