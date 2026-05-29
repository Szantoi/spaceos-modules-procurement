using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.ApprovePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.ConvertRequisitionToPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.RejectPurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Queries.GetRequisitionById;
using SpaceOS.Modules.Procurement.Application.Queries.GetRequisitions;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

public static class RequisitionEndpoints
{
    public static IEndpointRouteBuilder MapRequisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement/requisitions").RequireAuthorization("ManufacturerOnly");

        group.MapPost("/", CreateRequisition);
        group.MapGet("/", GetRequisitions);
        group.MapGet("/{id:guid}", GetRequisitionById);
        group.MapPost("/{id:guid}/approve", ApproveRequisition);
        group.MapPost("/{id:guid}/reject", RejectRequisition);
        group.MapPost("/{id:guid}/convert", ConvertRequisition);

        return app;
    }

    private static async Task<IResult> CreateRequisition(
        CreateRequisitionRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new CreatePurchaseRequisitionCommand(
            tenantId,
            GetSub(ctx),
            request.Lines.Select(l => new RequisitionLineRequest(l.MaterialCode, l.Quantity, l.EstimatedUnitPrice, l.PreferredSupplierId, l.Notes)).ToList(),
            request.Notes);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result, id => Results.Created($"/api/procurement/requisitions/{id}", new { id }));
    }

    private static async Task<IResult> GetRequisitions(
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetRequisitionsQuery(tenantId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> GetRequisitionById(
        Guid id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetRequisitionByIdQuery(tenantId, id), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ApproveRequisition(
        Guid id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new ApprovePurchaseRequisitionCommand(tenantId, id, GetSub(ctx), GetRoles(ctx));
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> RejectRequisition(
        Guid id,
        RejectRequisitionRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new RejectPurchaseRequisitionCommand(tenantId, id, GetSub(ctx), request.Reason);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ConvertRequisition(
        Guid id,
        ConvertRequisitionRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new ConvertRequisitionToPurchaseOrderCommand(
            tenantId, id, request.SupplierId, request.MaterialType,
            request.Quantity, request.UnitPrice, request.Currency ?? "HUF", GetSub(ctx),
            request.ExpectedDeliveryDate);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result, poId => Results.Ok(new { purchaseOrderId = poId }));
    }

    private static Guid GetTenantId(HttpContext ctx)
    {
        var claim = ctx.User?.FindFirst("tenant_id")?.Value ?? ctx.User?.FindFirst("tid")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static string GetSub(HttpContext ctx) =>
        ctx.User?.FindFirst("sub")?.Value ?? "unknown";

    private static List<string> GetRoles(HttpContext ctx) =>
        ctx.User?.Claims
            .Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();
}

public sealed record CreateRequisitionLineRequest(
    string MaterialCode,
    int Quantity,
    decimal? EstimatedUnitPrice,
    Guid? PreferredSupplierId,
    string? Notes);

public sealed record CreateRequisitionRequest(
    IReadOnlyList<CreateRequisitionLineRequest> Lines,
    string? Notes);

public sealed record RejectRequisitionRequest(string Reason);

public sealed record ConvertRequisitionRequest(
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string? Currency,
    DateTime? ExpectedDeliveryDate);
