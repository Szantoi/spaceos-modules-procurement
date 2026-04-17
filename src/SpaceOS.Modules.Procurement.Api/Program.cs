using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Api.Extensions;
using SpaceOS.Modules.Procurement.Infrastructure.Extensions;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProcurementApplication();
builder.Services.AddAuthentication().AddJwtBearer(opts => { opts.MapInboundClaims = false; });
builder.Services.AddAuthorization(opts =>
    opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));

var connectionString = builder.Configuration.GetConnectionString("Procurement")
    ?? "Host=localhost;Database=spaceos;Username=spaceos_app;Password=changeme";

builder.Services.AddProcurementInfrastructure(connectionString);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok("healthy")).AllowAnonymous();
app.MapGet("/health/ready", async (ProcurementDbContext db) =>
{
    await db.Database.CanConnectAsync();
    return Results.Ok("ready");
}).AllowAnonymous();

app.MapProcurementEndpoints();
app.MapInternalEndpoints();
app.Run();

public partial class Program { }
