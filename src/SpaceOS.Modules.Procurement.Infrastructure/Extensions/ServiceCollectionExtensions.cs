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
        services.AddDbContext<ProcurementDbContext>(options =>
            options.UseNpgsql(connectionString, npg =>
                npg.MigrationsHistoryTable("__EFMigrationsHistory", "spaceos_procurement")));

        services.AddScoped<IProcurementRepository, ProcurementRepository>();
        services.AddHttpContextAccessor();
        services.AddScoped<IProcurementTenantAccessor, HttpContextProcurementTenantAccessor>();
        services.AddScoped<IProcurementProvider, ProcurementProviderAdapter>();

        return services;
    }
}
