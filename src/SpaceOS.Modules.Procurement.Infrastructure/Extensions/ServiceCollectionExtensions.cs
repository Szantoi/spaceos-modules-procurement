using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpaceOS.Modules.Procurement.Contracts.Providers;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Domain.Services;
using SpaceOS.Modules.Procurement.Infrastructure.Adapters;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using SpaceOS.Modules.Procurement.Infrastructure.Retention;
using SpaceOS.Modules.Procurement.Infrastructure.Services;
using SpaceOS.Modules.Procurement.Infrastructure.Workers;

namespace SpaceOS.Modules.Procurement.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcurementInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<TenantSessionInterceptor>();

        services.AddDbContext<ProcurementDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "spaceos_procurement"));
            options.AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>());
        });

        services.AddScoped<IProcurementRepository, ProcurementRepository>();
        services.AddScoped<IProcurementV2Repository, ProcurementV2Repository>();
        services.AddScoped<IProcurementTenantAccessor, HttpContextProcurementTenantAccessor>();
        services.AddScoped<IProcurementProvider, ProcurementProviderAdapter>();

        // Domain services
        services.AddSingleton<IMatchPolicy, DefaultMatchPolicy>();
        services.AddSingleton<IAsnHashService, AsnHashService>();

        // BE-P-10: worker uses dedicated BYPASSRLS connection string (no TenantSessionInterceptor)
        var workerConnectionString = Environment.GetEnvironmentVariable("ProcurementWorkerConnectionString");
        if (string.IsNullOrEmpty(workerConnectionString))
        {
            // Fallback: same connection string, but still no interceptor — RLS may block until fixed
            workerConnectionString = connectionString;
        }
        services.AddSingleton<IProcurementWorkerDbContextFactory>(
            new ProcurementWorkerDbContextFactory(workerConnectionString));

        // Track D: outbox integration worker + HttpClient
        services.AddHttpClient("inventory-internal");
        services.AddHostedService<ProcurementIntegrationWorker>();

        // Track F: outbox/inbox retention cleanup background job
        services.AddHostedService<OutboxRetentionCleanupJob>();

        return services;
    }
}
