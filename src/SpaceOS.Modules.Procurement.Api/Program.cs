using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Api.Extensions;
using SpaceOS.Modules.Procurement.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProcurementApplication();
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization(opts =>
    opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));

var connectionString = builder.Configuration.GetConnectionString("Procurement")
    ?? "Host=localhost;Database=spaceos;Username=spaceos_app;Password=changeme";

builder.Services.AddProcurementInfrastructure(connectionString);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapProcurementEndpoints();
app.Run();

public partial class Program { }
