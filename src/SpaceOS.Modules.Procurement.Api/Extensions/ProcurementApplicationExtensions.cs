using Microsoft.Extensions.DependencyInjection;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;

namespace SpaceOS.Modules.Procurement.Api.Extensions;

public static class ProcurementApplicationExtensions
{
    public static IServiceCollection AddProcurementApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
            typeof(CreatePurchaseOrderCommandHandler).Assembly));
        return services;
    }
}
