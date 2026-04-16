using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpaceOS.Modules.Procurement.Contracts.Providers;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Adapters;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;

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
        services.AddScoped<IProcurementTenantAccessor, HttpContextProcurementTenantAccessor>();
        services.AddScoped<IProcurementProvider, ProcurementProviderAdapter>();

        return services;
    }
}
