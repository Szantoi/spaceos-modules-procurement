using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Workers;

/// <summary>
/// Creates a dedicated, unscoped <see cref="ProcurementDbContext"/> for the background worker.
/// The worker must not share the request-scoped DbContext (which has TenantSessionInterceptor
/// relying on JWT claims). The worker sets app.current_tenant_id manually per message.
/// </summary>
public interface IProcurementWorkerDbContextFactory
{
    /// <summary>Creates a fresh DbContext for one processing iteration.</summary>
    Task<ProcurementDbContext> CreateAsync(CancellationToken ct);
}

/// <summary>
/// Uses the BYPASSRLS worker connection string — no TenantSessionInterceptor.
/// The worker sets set_config('app.current_tenant_id', ...) per message explicitly.
/// </summary>
internal sealed class ProcurementWorkerDbContextFactory(string workerConnectionString)
    : IProcurementWorkerDbContextFactory
{
    public Task<ProcurementDbContext> CreateAsync(CancellationToken ct)
    {
        var opts = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql(workerConnectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "spaceos_procurement"))
            .Options;

        return Task.FromResult(new ProcurementDbContext(opts));
    }
}
