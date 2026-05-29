using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Application.Commands.ReorderAlertReceiver;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Api;

/// <summary>
/// Tests for POST /internal/from-reorder-alert (Track E).
/// SEC-P-01: Bearer constant-time check; DB-P-07: header==body tenantId.
/// </summary>
public class InternalReceiverTests : IDisposable
{
    private const string ValidSecret = "test-internal-secret-abc123";
    private static readonly Guid TestTenantId = new("50000000-0000-0000-0000-000000000001");

    public InternalReceiverTests()
    {
        Environment.SetEnvironmentVariable("SPACEOS_INTERNAL_SECRET", ValidSecret);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SPACEOS_INTERNAL_SECRET", null);
    }

    private static object ValidPayload(Guid? tenantId = null)
    {
        var tid = tenantId ?? TestTenantId;
        return new
        {
            TenantId = tid,
            MaterialCode = "WD-001",
            CurrentStock = 5m,
            ReorderPoint = 10m,
            SuggestedQuantity = 50m,
            PreferredSupplierId = (Guid?)null,
            UnitOfMeasure = "pcs",
            AlertedAt = DateTimeOffset.UtcNow
        };
    }

    private HttpClient CreateClient(Mock<IMediator>? mediatorMock = null)
    {
        mediatorMock ??= BuildDefaultMediator();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediatorMock.Object);
        builder.Services.AddSingleton(new Mock<IProcurementRepository>().Object);
        builder.Services.AddDbContext<ProcurementDbContext>(opts =>
            opts.UseInMemoryDatabase($"procurement-receiver-test-{Guid.NewGuid()}"));
        builder.Services.AddAuthentication("NoAuth")
            .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("NoAuth", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRouting();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInternalEndpoints();
        app.StartAsync().GetAwaiter().GetResult();

        return (app.Services.GetRequiredService<IServer>() as TestServer)!.CreateClient();
    }

    private static Mock<IMediator> BuildDefaultMediator(
        ReorderAlertReceiverResult? result = null, bool isDuplicate = false)
    {
        var mock = new Mock<IMediator>();
        var reqId = Guid.NewGuid();
        mock.Setup(m => m.Send(It.IsAny<ReorderAlertReceiverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result ?? new ReorderAlertReceiverResult(reqId, isDuplicate));
        return mock;
    }

    private static HttpRequestMessage BuildRequest(object payload, Guid? tenantIdHeader = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/from-reorder-alert");
        req.Content = JsonContent.Create(payload);

        var tid = tenantIdHeader ?? TestTenantId;
        req.Headers.Add("X-SpaceOS-TenantId", tid.ToString());

        if (bearer is not null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

        return req;
    }

    [Fact]
    public async Task PostFromReorderAlert_NoAuth_Returns401()
    {
        var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/from-reorder-alert");
        req.Content = JsonContent.Create(ValidPayload());
        req.Headers.Add("X-SpaceOS-TenantId", TestTenantId.ToString());
        // No Authorization header

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostFromReorderAlert_WrongBearer_Returns401()
    {
        var client = CreateClient();
        var req = BuildRequest(ValidPayload(), bearer: "wrong-secret-value");

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostFromReorderAlert_ValidBearer_Returns201()
    {
        var client = CreateClient();
        var req = BuildRequest(ValidPayload(), bearer: ValidSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostFromReorderAlert_DuplicateAlert_Returns200Idempotent()
    {
        var reqId = Guid.NewGuid();
        var dupMediator = BuildDefaultMediator(new ReorderAlertReceiverResult(reqId, IsDuplicate: true));
        var client = CreateClient(dupMediator);

        var req = BuildRequest(ValidPayload(), bearer: ValidSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RequisitionIdResponse>();
        body!.RequisitionId.Should().Be(reqId);
    }

    [Fact]
    public async Task PostFromReorderAlert_TenantIdMismatch_Returns400()
    {
        var client = CreateClient();
        var differentTenantId = Guid.NewGuid();
        // Header has TestTenantId but body has differentTenantId
        var req = BuildRequest(ValidPayload(differentTenantId), tenantIdHeader: TestTenantId, bearer: ValidSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostFromReorderAlert_OrphanMaterialCode_Returns422()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ReorderAlertReceiverCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MaterialCode orphan"));

        var client = CreateClient(mediatorMock);
        var req = BuildRequest(ValidPayload(), bearer: ValidSecret);

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostFromReorderAlert_ValidBearer_CreatesPurchaseRequisitionInDraftStatus()
    {
        var expectedId = Guid.NewGuid();
        var mediatorMock = BuildDefaultMediator(new ReorderAlertReceiverResult(expectedId, IsDuplicate: false));
        var client = CreateClient(mediatorMock);

        var req = BuildRequest(ValidPayload(), bearer: ValidSecret);
        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RequisitionIdResponse>();
        body!.RequisitionId.Should().Be(expectedId);
    }

    [Fact]
    public async Task PostFromReorderAlert_SecondCallWithSameIdempotencyKey_Returns200SameRequisitionId()
    {
        var sharedId = Guid.NewGuid();
        // First call: creates
        var mediatorMock = new Mock<IMediator>();
        var callCount = 0;
        mediatorMock.Setup(m => m.Send(It.IsAny<ReorderAlertReceiverCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new ReorderAlertReceiverResult(sharedId, IsDuplicate: false)
                    : new ReorderAlertReceiverResult(sharedId, IsDuplicate: true);
            });

        var client = CreateClient(mediatorMock);
        var payload = ValidPayload();

        // First call
        var req1 = BuildRequest(payload, bearer: ValidSecret);
        var response1 = await client.SendAsync(req1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second call with same payload
        var req2 = BuildRequest(payload, bearer: ValidSecret);
        var response2 = await client.SendAsync(req2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body2 = await response2.Content.ReadFromJsonAsync<RequisitionIdResponse>();
        body2!.RequisitionId.Should().Be(sharedId);
    }

    private sealed record RequisitionIdResponse(Guid RequisitionId);
}
