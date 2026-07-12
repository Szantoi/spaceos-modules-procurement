using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.ActivatePriceList;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePriceList;
using SpaceOS.Modules.Procurement.Application.Commands.UpdatePriceList;
using SpaceOS.Modules.Procurement.Application.Queries.GetBestPrice;
using SpaceOS.Modules.Procurement.Application.Queries.GetPriceLists;
using SpaceOS.Modules.Procurement.Application.Queries.GetPriceListsBySupplier;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

public static class PriceListEndpoints
{
    public static IEndpointRouteBuilder MapPriceListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement/price-lists").RequireAuthorization("ManufacturerOnly");

        group.MapPost("/", CreatePriceList);
        group.MapPost("/{id:guid}/activate", ActivatePriceList);
        group.MapGet("/", GetPriceLists);
        group.MapGet("/best-price", GetBestPrice);

        // BE-PROC-001: Supplier self-service price list endpoints
        var supplierGroup = app.MapGroup("/api/procurement/suppliers/{supplierId:guid}/price-list")
            .RequireAuthorization("ManufacturerOnly");

        supplierGroup.MapPost("/", CreatePriceListForSupplier);
        supplierGroup.MapPut("/{id:guid}", UpdatePriceList);
        supplierGroup.MapPost("/{id:guid}/activate", ActivatePriceListForSupplier);
        supplierGroup.MapGet("/", GetPriceListsBySupplier);

        return app;
    }

    private static async Task<IResult> CreatePriceList(
        CreatePriceListRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new CreatePriceListCommand(
            tenantId,
            request.SupplierId,
            request.Currency,
            request.ValidFrom,
            request.ValidTo,
            request.Entries.Select(e => new Application.Commands.CreatePriceList.PriceListEntryRequest(e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity)).ToList());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result, id => Results.Created($"/api/procurement/price-lists/{id}", new { id }));
    }

    private static async Task<IResult> ActivatePriceList(
        Guid id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(
            new ActivatePriceListCommand(tenantId, id, GetSub(ctx), GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> GetPriceLists(
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetPriceListsQuery(tenantId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> GetBestPrice(
        string material,
        int qty,
        string currency,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetBestPriceQuery(tenantId, material, qty, currency), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    // BE-PROC-001: Supplier self-service endpoints
    private static async Task<IResult> CreatePriceListForSupplier(
        Guid supplierId,
        CreatePriceListRequestBody request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new CreatePriceListCommand(
            tenantId,
            supplierId,
            request.Currency,
            request.ValidFrom,
            request.ValidTo,
            request.Entries.Select(e => new Application.Commands.CreatePriceList.PriceListEntryRequest(
                e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity)).ToList());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result, id => Results.Created(
            $"/api/procurement/suppliers/{supplierId}/price-list/{id}", new { id }));
    }

    private static async Task<IResult> UpdatePriceList(
        Guid supplierId,
        Guid id,
        UpdatePriceListRequestBody request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var command = new UpdatePriceListCommand(
            tenantId,
            id,
            request.ValidFrom,
            request.ValidTo,
            request.Entries.Select(e => new Application.Commands.UpdatePriceList.PriceListEntryRequest(
                e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity)).ToList());

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> ActivatePriceListForSupplier(
        Guid supplierId,
        Guid id,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(
            new ActivatePriceListCommand(tenantId, id, GetSub(ctx), GetRoles(ctx)), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> GetPriceListsBySupplier(
        Guid supplierId,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetPriceListsBySupplierQuery(tenantId, supplierId), ct).ConfigureAwait(false);
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

public sealed record PriceListEntryRequestBody(
    string MaterialCode,
    decimal UnitPrice,
    int MinQuantity = 1,
    int? MaxQuantity = null);

public sealed record CreatePriceListRequest(
    Guid SupplierId,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PriceListEntryRequestBody> Entries);

public sealed record CreatePriceListRequestBody(
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PriceListEntryRequestBody> Entries);

public sealed record UpdatePriceListRequestBody(
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PriceListEntryRequestBody> Entries);
