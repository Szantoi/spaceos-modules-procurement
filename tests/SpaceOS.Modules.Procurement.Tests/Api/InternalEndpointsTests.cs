using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Api;

public class InternalEndpointsTests
{
    private static readonly Guid TestTenantId = new("aaaaaaaa-0000-0000-0000-000000000001");
    private const string InternalHeader = "X-SpaceOS-Internal";

    private HttpClient CreateClient(
        Mock<IProcurementRepository>? repoMock = null,
        string allowlist = "")
    {
        repoMock ??= new Mock<IProcurementRepository>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(repoMock.Object);
        builder.Services.AddDbContext<ProcurementDbContext>(opts =>
            opts.UseInMemoryDatabase($"procurement-internal-test-{Guid.NewGuid()}"));
        builder.Services.AddAuthentication("NoAuth")
            .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("NoAuth", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRouting();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["TEST_TENANT_ALLOWLIST"] = allowlist });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInternalEndpoints();
        app.StartAsync().GetAwaiter().GetResult();

        var testServer = app.Services.GetRequiredService<IServer>() as TestServer;
        return testServer!.CreateClient();
    }

    [Fact]
    public async Task DeleteByTenant_MissingInternalHeader_Returns403()
    {
        var client = CreateClient(allowlist: TestTenantId.ToString());

        var response = await client.DeleteAsync(
            $"/internal/purchase-orders/by-tenant/{TestTenantId}?confirm=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteByTenant_MissingConfirm_Returns400()
    {
        var client = CreateClient(allowlist: TestTenantId.ToString());
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/internal/purchase-orders/by-tenant/{TestTenantId}");
        request.Headers.Add(InternalHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteByTenant_InvalidGuid_Returns400()
    {
        var client = CreateClient(allowlist: "not-a-guid");
        var request = new HttpRequestMessage(HttpMethod.Delete,
            "/internal/purchase-orders/by-tenant/not-a-guid?confirm=true");
        request.Headers.Add(InternalHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteByTenant_TenantNotInAllowlist_Returns403()
    {
        var client = CreateClient(allowlist: "bbbbbbbb-0000-0000-0000-000000000002");
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/internal/purchase-orders/by-tenant/{TestTenantId}?confirm=true");
        request.Headers.Add(InternalHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteByTenant_ValidRequest_Returns200WithCounts()
    {
        var repoMock = new Mock<IProcurementRepository>();
        repoMock.Setup(r => r.DeleteAllByTenantAsync(TestTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantDeletedCounts(3, 7));

        var client = CreateClient(repoMock, allowlist: TestTenantId.ToString());
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/internal/purchase-orders/by-tenant/{TestTenantId}?confirm=true");
        request.Headers.Add(InternalHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<DeleteByTenantResponse>();
        body.Should().NotBeNull();
        body!.TenantId.Should().Be(TestTenantId.ToString());
        body.DeletedCounts.PurchaseOrders.Should().Be(3);
        body.DeletedCounts.Deliveries.Should().Be(7);
    }

    [Fact]
    public async Task DeleteByTenant_EmptyAllowlist_Returns403()
    {
        var client = CreateClient(allowlist: "");
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/internal/purchase-orders/by-tenant/{TestTenantId}?confirm=true");
        request.Headers.Add(InternalHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record DeletedCountsDto(int PurchaseOrders, int Deliveries);
    private sealed record DeleteByTenantResponse(string TenantId, DeletedCountsDto DeletedCounts);
}
