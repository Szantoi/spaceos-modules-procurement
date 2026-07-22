using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.CancelPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.ConfirmPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.CreateSupplier;
using SpaceOS.Modules.Procurement.Application.Commands.MarkPurchaseOrderShipped;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Commands.SubmitPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrders;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Application.Queries.GetSupplierPrices;
using SpaceOS.Modules.Procurement.Application.Queries.GetSuppliers;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

public static class ProcurementEndpoints
{
    public static IEndpointRouteBuilder MapProcurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement").RequireAuthorization("ManufacturerOnly");

        group.MapPost("/suppliers", CreateSupplier);
        group.MapGet("/suppliers", GetSuppliers);
        group.MapGet("/orders", GetOrders);
        group.MapPost("/orders", CreatePurchaseOrder);
        group.MapGet("/orders/{id}", GetOrderStatus);
        group.MapGet("/prices", GetSupplierPrices);
        group.MapPost("/deliveries", RecordDelivery);

        // WORLDS-PROC-PO-FSM: portal-usable PurchaseOrder transitions. Exact paths —
        // see docs/knowledge/architecture: PO_FSM_API.md (module-local contract doc).
        group.MapPost("/orders/{id}/submit", SubmitPurchaseOrder);
        group.MapPost("/orders/{id}/confirm", ConfirmPurchaseOrder);
        group.MapPost("/orders/{id}/ship", MarkPurchaseOrderShipped);
        group.MapPost("/orders/{id}/deliver", DeliverPurchaseOrder);
        group.MapPost("/orders/{id}/cancel", CancelPurchaseOrder);

        return app;
    }

    private static async Task<IResult> GetOrders(
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetOrdersQuery(tenantId), ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Errors);
    }

    private static async Task<IResult> CreateSupplier(
        CreateSupplierRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new CreateSupplierCommand(tenantId, request.Name, request.Email ?? string.Empty, request.Phone ?? string.Empty, request.Address ?? string.Empty);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Created($"/api/procurement/suppliers/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Errors);
    }

    private static async Task<IResult> GetSuppliers(
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetSuppliersQuery(tenantId), ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Errors);
    }

    private static async Task<IResult> CreatePurchaseOrder(
        CreatePurchaseOrderRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new CreatePurchaseOrderCommand(
            tenantId,
            request.SupplierId,
            request.MaterialType,
            request.Quantity,
            request.UnitPrice,
            request.Currency ?? "HUF",
            request.ExpectedDeliveryDate);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(new { id = result.Value }) : Results.BadRequest(result.Errors);
    }

    private static async Task<IResult> GetOrderStatus(
        string id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var result = await mediator.Send(new GetOrderStatusQuery(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> SubmitPurchaseOrder(
        string id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var result = await mediator.Send(new SubmitPurchaseOrderCommand(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ConfirmPurchaseOrder(
        string id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var result = await mediator.Send(new ConfirmPurchaseOrderCommand(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> MarkPurchaseOrderShipped(
        string id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var result = await mediator.Send(new MarkPurchaseOrderShippedCommand(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> CancelPurchaseOrder(
        string id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var result = await mediator.Send(new CancelPurchaseOrderCommand(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    /// <summary>
    /// WORLDS-PROC-PO-FSM: portal-facing wrapper for the Shipped → Delivered transition.
    /// Reuses <see cref="RecordDeliveryCommand"/> / <see cref="RecordDeliveryCommandHandler"/>
    /// unchanged (same outbox + inventory-inbound transaction) — this endpoint only adds the
    /// route-per-aggregate shape (<c>/orders/{id}/deliver</c>) and, on success, fetches the
    /// fresh order DTO so every transition endpoint in this group has the same response contract.
    /// </summary>
    private static async Task<IResult> DeliverPurchaseOrder(
        string id,
        DeliverPurchaseOrderRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        if (!Guid.TryParse(id, out var orderId))
            return Results.BadRequest(new { error = $"'{id}' is not a valid order id." });

        var command = new RecordDeliveryCommand(
            tenantId,
            orderId,
            request.ReceivedQuantity,
            request.Notes,
            request.RecordedBy ?? "system");

        var deliverResult = await mediator.Send(command, ct).ConfigureAwait(false);
        if (!deliverResult.IsSuccess)
            return ResultToHttp.Map(deliverResult);

        var freshOrder = await mediator.Send(new GetOrderStatusQuery(tenantId, orderId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(freshOrder);
    }

    private static async Task<IResult> GetSupplierPrices(
        string? materialType,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetSupplierPricesQuery(tenantId, materialType ?? string.Empty), ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Errors);
    }

    private static async Task<IResult> RecordDelivery(
        RecordDeliveryRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new RecordDeliveryCommand(
            tenantId,
            request.PurchaseOrderId,
            request.ReceivedQuantity,
            request.Notes,
            request.RecordedBy ?? "system");

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Errors);
    }

    private static Guid GetTenantId(HttpContext ctx)
    {
        var claim = ctx.User?.FindFirst("tid")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record CreateSupplierRequest(string Name, string? Email, string? Phone, string? Address, string? Notes);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string? Currency,
    DateTime? ExpectedDeliveryDate);

public sealed record RecordDeliveryRequest(
    Guid PurchaseOrderId,
    decimal ReceivedQuantity,
    string? Notes,
    string? RecordedBy);

/// <summary>Body for <c>POST /api/procurement/orders/{id}/deliver</c> — the order id is the route param.</summary>
public sealed record DeliverPurchaseOrderRequest(
    decimal ReceivedQuantity,
    string? Notes,
    string? RecordedBy);
