using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpaceOS.Modules.Procurement.Application.Commands.UpdateMatchPolicy;
using SpaceOS.Modules.Procurement.Application.Queries.GetMatchPolicy;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

public static class MatchPolicyEndpoints
{
    public static IEndpointRouteBuilder MapMatchPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procurement/match-policy").RequireAuthorization("ManufacturerOnly");

        group.MapGet("/", GetMatchPolicy);
        group.MapPut("/", UpdateMatchPolicy);

        return app;
    }

    private static async Task<IResult> GetMatchPolicy(
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(new GetMatchPolicyQuery(tenantId), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static async Task<IResult> UpdateMatchPolicy(
        UpdateMatchPolicyRequest request,
        IMediator mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var tenantId = GetTenantId(ctx);
        if (tenantId == Guid.Empty) return Results.Unauthorized();

        var result = await mediator.Send(
            new UpdateMatchPolicyCommand(tenantId, request.PriceTolerancePct, request.QuantityToleranceAbs), ct).ConfigureAwait(false);
        return ResultToHttp.Map(result);
    }

    private static Guid GetTenantId(HttpContext ctx)
    {
        var claim = ctx.User?.FindFirst("tenant_id")?.Value ?? ctx.User?.FindFirst("tid")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public sealed record UpdateMatchPolicyRequest(
    decimal PriceTolerancePct,
    int QuantityToleranceAbs);
