using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoiceWithVariance;
using SpaceOS.Modules.Procurement.Application.Commands.DisputeInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.RunMatch;
using SpaceOS.Modules.Procurement.Application.Queries.GetInvoiceById;
using SpaceOS.Modules.Procurement.Application.Queries.GetInvoices;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement/invoices").RequireAuthorization("ManufacturerOnly");

        group.MapPost("/", ReceiveInvoice);
        group.MapGet("/", GetInvoices);
        group.MapGet("/{id:guid}", GetInvoiceById);
        group.MapPost("/{id:guid}/match", RunMatch);
        group.MapPost("/{id:guid}/approve", ApproveInvoice);
        group.MapPost("/{id:guid}/approve-with-variance", ApproveWithVariance);
        group.MapPost("/{id:guid}/dispute", DisputeInvoice);

        return app;
    }

    private static async Task<IResult> ReceiveInvoice(
        ReceiveInvoiceRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new ReceiveInvoiceCommand(
            tenantId,
            request.SupplierId,
            request.PurchaseOrderId,
            request.SupplierInvoiceNumber,
            request.InvoiceDate,
            request.DueDate,
            request.Currency,
            GetSub(ctx),
            request.Lines.Select(l => new InvoiceLineRequest(l.MaterialCode, l.PurchaseOrderLineId, l.Quantity, l.UnitPrice, l.LineNetAmount, l.LineVatAmount)).ToList());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result, id => Results.Created($"/api/procurement/invoices/{id}", new { id }));
    }

    private static async Task<IResult> GetInvoices(IMediator mediator, HttpContext ctx, CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetInvoicesQuery(tenantId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> GetInvoiceById(Guid id, IMediator mediator, HttpContext ctx, CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetInvoiceByIdQuery(tenantId, id), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> RunMatch(Guid id, IMediator mediator, HttpContext ctx, CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new RunMatchCommand(tenantId, id, GetSub(ctx), GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ApproveInvoice(Guid id, IMediator mediator, HttpContext ctx, CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new ApproveInvoiceCommand(tenantId, id, GetSub(ctx), GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ApproveWithVariance(Guid id, IMediator mediator, HttpContext ctx, CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new ApproveInvoiceWithVarianceCommand(tenantId, id, GetSub(ctx), GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> DisputeInvoice(
        Guid id,
        DisputeInvoiceRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new DisputeInvoiceCommand(tenantId, id, GetSub(ctx), request.Reason, GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static Guid GetTenantId(HttpContext ctx)
    {
        var claim = ctx.User?.FindFirst("tenant_id")?.Value ?? ctx.User?.FindFirst("tid")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static string GetSub(HttpContext ctx) => ctx.User?.FindFirst("sub")?.Value ?? "unknown";

    private static List<string> GetRoles(HttpContext ctx) =>
        ctx.User?.Claims
            .Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();
}

public sealed record InvoiceLineRequestBody(
    string MaterialCode,
    Guid? PurchaseOrderLineId,
    int Quantity,
    decimal UnitPrice,
    decimal LineNetAmount,
    decimal LineVatAmount);

public sealed record ReceiveInvoiceRequest(
    Guid SupplierId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Currency,
    IReadOnlyList<InvoiceLineRequestBody> Lines);

public sealed record DisputeInvoiceRequest(string Reason);
