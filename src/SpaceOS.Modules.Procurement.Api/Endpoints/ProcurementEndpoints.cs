using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.CreateSupplier;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
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
        group.MapGet("/orders/{id:guid}", GetOrderStatus);
        group.MapGet("/prices", GetSupplierPrices);
        group.MapPost("/deliveries", RecordDelivery);

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
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderStatusQuery(id), ct).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Errors);
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
