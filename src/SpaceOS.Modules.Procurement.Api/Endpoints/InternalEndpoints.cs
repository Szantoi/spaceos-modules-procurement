using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpaceOS.Modules.Procurement.Api.Security;
using SpaceOS.Modules.Procurement.Application.Commands.ReorderAlertReceiver;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Api.Endpoints;

/// <summary>
/// Internal endpoints — only callable with X-SpaceOS-Internal header.
/// Used by the Orchestrator for test data reset (BE-TEST-06).
/// SEC-TS-01: TEST_TENANT_ALLOWLIST env var restricts which tenants can be reset.
/// </summary>
public static class InternalEndpoints
{
    private const string InternalHeader = "X-SpaceOS-Internal";
    private const string LoggerCategory = "SpaceOS.Procurement.Internal";

    public static void MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/internal/purchase-orders/by-tenant/{tenantId}", async (
            string tenantId,
            bool? confirm,
            HttpContext ctx,
            IProcurementRepository repo,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger(LoggerCategory);

            // SEC-TS-01: X-SpaceOS-Internal header required
            if (!ctx.Request.Headers.ContainsKey(InternalHeader))
            {
                logger.LogWarning("InternalDeleteByTenant: missing {Header} from {RemoteIp}",
                    InternalHeader, ctx.Connection.RemoteIpAddress);
                return Results.Json(
                    new { error = "Forbidden", message = "Missing X-SpaceOS-Internal header" },
                    statusCode: 403);
            }

            // confirm=true required to prevent accidental deletes
            if (confirm != true)
                return Results.BadRequest(new { error = "Bad request", message = "Missing confirm=true parameter" });

            // Validate tenantId GUID format
            if (!Guid.TryParse(tenantId, out _))
                return Results.BadRequest(new { error = "Bad request", message = "Invalid tenantId format" });

            // SEC-TS-01: allowlist check — defense in depth against compromised Orchestrator
            var allowlistRaw = config["TEST_TENANT_ALLOWLIST"]
                ?? Environment.GetEnvironmentVariable("TEST_TENANT_ALLOWLIST")
                ?? string.Empty;

            var allowlist = allowlistRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowlist.Contains(tenantId))
            {
                logger.LogWarning(
                    "InternalDeleteByTenant: tenant {TenantId} not in TEST_TENANT_ALLOWLIST — rejected",
                    tenantId);
                return Results.Json(
                    new { error = "Forbidden", message = "Tenant not in test allowlist" },
                    statusCode: 403);
            }

            var tenantGuid = Guid.Parse(tenantId);

            // Resolve DbContext from request scope (not via parameter — DELETE has no body inference).
            var dbContext = ctx.RequestServices.GetRequiredService<ProcurementDbContext>();

            // Pin set_config and DeleteAllByTenantAsync to the same physical connection so the
            // GUC value is not lost when the pool returns a different connection for the repo call.
            if (dbContext.Database.IsRelational())
                await dbContext.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

            TenantDeletedCounts counts;
            try
            {
                if (dbContext.Database.IsRelational())
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('app.current_tenant_id', {0}, false)",
                        tenantGuid.ToString()).ConfigureAwait(false);

                counts = await repo.DeleteAllByTenantAsync(tenantGuid, ct).ConfigureAwait(false);
            }
            finally
            {
                if (dbContext.Database.IsRelational())
                    await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }

            logger.LogInformation(
                "InternalDeleteByTenant: tenant {TenantId} reset — deleted {Orders} orders, {Deliveries} deliveries",
                tenantId, counts.PurchaseOrders, counts.Deliveries);

            return Results.Ok(new
            {
                tenantId,
                deletedCounts = new
                {
                    purchaseOrders = counts.PurchaseOrders,
                    deliveries = counts.Deliveries
                }
            });
        })
        .AllowAnonymous()
        .WithTags("Internal");

        // Track E: from-reorder-alert receiver
        // IMediator resolved from RequestServices (not lambda parameter) to avoid startup DI resolution.
        app.MapPost("/internal/from-reorder-alert", async (
            ReorderAlertRequest request,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            // DB-P-07: X-SpaceOS-TenantId header must equal body tenantId
            var headerTenantRaw = ctx.Request.Headers["X-SpaceOS-TenantId"].ToString();
            if (!Guid.TryParse(headerTenantRaw, out var headerTenantId))
                return Results.BadRequest(new { error = "Bad request", message = "Invalid or missing X-SpaceOS-TenantId header" });

            if (headerTenantId != request.TenantId)
                return Results.BadRequest(new { error = "Bad request", message = "X-SpaceOS-TenantId header does not match body tenantId" });

            if (string.IsNullOrWhiteSpace(request.MaterialCode))
                return Results.UnprocessableEntity(new { error = "Unprocessable", message = "MaterialCode is required" });

            var mediator = ctx.RequestServices.GetRequiredService<IMediator>();
            var dbContext = ctx.RequestServices.GetRequiredService<ProcurementDbContext>();

            var command = new ReorderAlertReceiverCommand(
                request.TenantId,
                request.MaterialCode,
                request.CurrentStock,
                request.ReorderPoint,
                request.SuggestedQuantity,
                request.PreferredSupplierId,
                request.UnitOfMeasure ?? "pcs",
                request.AlertedAt);

            // DB-P-07: pin GUC to this connection so RETURNING sees the inserted row (RLS USING check).
            // Same pattern as the delete-by-tenant endpoint above.
            if (dbContext.Database.IsRelational())
                await dbContext.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

            ReorderAlertReceiverResult result;
            try
            {
                if (dbContext.Database.IsRelational())
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('app.current_tenant_id', {0}, false)",
                        request.TenantId.ToString()).ConfigureAwait(false);

                result = await mediator.Send(command, ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("MaterialCode"))
            {
                // SEC-P-10: orphan materialCode → 422 (not 5xx)
                return Results.UnprocessableEntity(new { error = "Unprocessable", message = ex.Message });
            }
            finally
            {
                if (dbContext.Database.IsRelational())
                    await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }

            if (result.IsDuplicate)
                return Results.Ok(new { requisitionId = result.RequisitionId });

            return Results.Created(
                $"/api/procurement/requisitions/{result.RequisitionId}",
                new { requisitionId = result.RequisitionId });
        })
        .AddEndpointFilter<InternalBearerEndpointFilter>()
        .AllowAnonymous()
        .WithTags("Internal");
    }
}

/// <summary>Request body for the from-reorder-alert internal endpoint.</summary>
public sealed record ReorderAlertRequest(
    Guid TenantId,
    string MaterialCode,
    decimal CurrentStock,
    decimal ReorderPoint,
    decimal SuggestedQuantity,
    Guid? PreferredSupplierId,
    string? UnitOfMeasure,
    DateTimeOffset AlertedAt);
