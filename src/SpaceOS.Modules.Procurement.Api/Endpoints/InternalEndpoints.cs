using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    }
}
