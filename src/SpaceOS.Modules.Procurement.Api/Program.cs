using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Api.Extensions;
using SpaceOS.Modules.Procurement.Infrastructure.Extensions;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProcurementApplication();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();

var jwtAuthority = builder.Configuration["Jwt:Authority"]
    ?? Environment.GetEnvironmentVariable("JWT_AUTHORITY");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? "kernel-api";

if (builder.Environment.IsProduction())
    ArgumentNullException.ThrowIfNullOrEmpty(jwtAuthority,
        "Jwt:Authority / JWT_AUTHORITY must be configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;
        opts.Authority = jwtAuthority;
        opts.Audience = jwtAudience;
        opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.FromSeconds(30),
            NameClaimType    = "preferred_username",
            RoleClaimType    = ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization(opts =>
    opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));

var connectionString = builder.Configuration.GetConnectionString("Procurement")
    ?? "Host=localhost;Database=spaceos;Username=spaceos_app;Password=changeme";

builder.Services.AddProcurementInfrastructure(connectionString);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok("healthy")).AllowAnonymous();
app.MapGet("/health/ready", async (ProcurementDbContext db) =>
{
    await db.Database.CanConnectAsync();
    return Results.Ok("ready");
}).AllowAnonymous();

app.MapProcurementEndpoints();
app.MapRequisitionEndpoints();
app.MapInvoiceEndpoints();
app.MapPriceListEndpoints();
app.MapMatchPolicyEndpoints();
app.MapInternalEndpoints();
app.Run();

public partial class Program { }
