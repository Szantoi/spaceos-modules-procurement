using Ardalis.Result;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceOS.Modules.Procurement.Application.Commands.CreateSubcontractOrder;
using SpaceOS.Modules.Procurement.Application.Commands.AcceptSubcontractOrder;
using SpaceOS.Modules.Procurement.Application.Commands.RejectSubcontractOrder;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/procurement")]
public class SubcontractsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ISubcontractRepository _repository;

    public SubcontractsController(IMediator mediator, ISubcontractRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    /// <summary>
    /// Creates a new subcontract order (tenant action).
    /// </summary>
    [HttpPost("subcontracts")]
    public async Task<IActionResult> Create([FromBody] CreateSubcontractRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = User.Identity?.Name ?? "unknown";

        var command = new CreateSubcontractOrderCommand(
            tenantId,
            request.SupplierId,
            request.WorkDescription,
            request.EstimatedCost,
            request.Currency,
            request.Deadline,
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
    /// Gets subcontract orders for a specific supplier (supplier portal).
    /// </summary>
    [HttpGet("suppliers/{supplierId:guid}/subcontracts")]
    public async Task<IActionResult> ListForSupplier(Guid supplierId, CancellationToken ct)
    {
        var orders = await _repository.GetBySupplierAsync(supplierId, null, ct).ConfigureAwait(false);
        return Ok(orders.Select(o => new
        {
            o.Id,
            o.OrderNumber,
            o.Status,
            o.WorkDescription,
            o.EstimatedCost,
            o.Currency,
            o.Deadline,
            o.CreatedAt
        }));
    }

    /// <summary>
    /// Partner accepts a subcontract order.
    /// </summary>
    [HttpPost("suppliers/{supplierId:guid}/subcontracts/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid supplierId, Guid id, CancellationToken ct)
    {
        // Verify the order belongs to this supplier
        var order = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (order is null)
            return NotFound();
        if (order.SupplierId != supplierId)
            return Forbid();

        var command = new AcceptSubcontractOrderCommand(id);
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
    /// Partner rejects a subcontract order.
    /// </summary>
    [HttpPost("suppliers/{supplierId:guid}/subcontracts/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid supplierId, Guid id, [FromBody] RejectRequest request, CancellationToken ct)
    {
        // Verify the order belongs to this supplier
        var order = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (order is null)
            return NotFound();
        if (order.SupplierId != supplierId)
            return Forbid();

        var command = new RejectSubcontractOrderCommand(id, request.Reason);
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

public record CreateSubcontractRequest(
    Guid SupplierId,
    string WorkDescription,
    decimal EstimatedCost,
    string? Currency,
    DateTime Deadline);

public record RejectRequest(string Reason);
