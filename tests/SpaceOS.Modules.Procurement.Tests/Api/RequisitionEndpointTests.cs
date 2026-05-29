using System.Net;
using System.Net.Http.Json;
using Ardalis.Result;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SpaceOS.Modules.Procurement.Api.Endpoints;
using SpaceOS.Modules.Procurement.Application.Commands.ApprovePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Application.Queries.GetRequisitions;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Api;

public class RequisitionEndpointTests
{
    private HttpClient CreateAuthClient(Mock<IMediator> mediatorMock)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediatorMock.Object);
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(opts =>
            opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));
        builder.Services.AddRouting();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRequisitionEndpoints();
        app.StartAsync().GetAwaiter().GetResult();

        var testServer = app.Services.GetRequiredService<IServer>() as TestServer;
        var client = testServer!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        return client;
    }

    private HttpClient CreateNoAuthClient(Mock<IMediator> mediatorMock)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediatorMock.Object);
        builder.Services.AddAuthentication("NoAuth")
            .AddScheme<AuthenticationSchemeOptions, NoAuthHandler>("NoAuth", _ => { });
        builder.Services.AddAuthorization(opts =>
            opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));
        builder.Services.AddRouting();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRequisitionEndpoints();
        app.StartAsync().GetAwaiter().GetResult();

        return (app.Services.GetRequiredService<IServer>() as TestServer)!.CreateClient();
    }

    [Fact]
    public async Task PostRequisitions_WithAuth_Returns201()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<CreatePurchaseRequisitionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        var client = CreateAuthClient(mediatorMock);
        var payload = new
        {
            Lines = new[] { new { MaterialCode = "WD-001", Quantity = 10, EstimatedUnitPrice = (decimal?)null, PreferredSupplierId = (Guid?)null, Notes = (string?)null } },
            Notes = (string?)null
        };

        var response = await client.PostAsJsonAsync("/api/procurement/requisitions", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostRequisitions_NoAuth_Returns401()
    {
        var mediatorMock = new Mock<IMediator>();
        var client = CreateNoAuthClient(mediatorMock);

        var response = await client.PostAsJsonAsync("/api/procurement/requisitions", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostApproveRequisition_WithoutApproverRole_Returns403()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ApprovePurchaseRequisitionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden());

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/requisitions/{Guid.NewGuid()}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostApproveRequisition_WithApproverRole_Returns200()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ApprovePurchaseRequisitionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/requisitions/{Guid.NewGuid()}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRequisitions_WithAuth_Returns200List()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetRequisitionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<RequisitionDto>>.Success(
                new List<RequisitionDto>
                {
                    new(Guid.NewGuid(), TestAuthHandler.TestTenantId, "PR-2026-001",
                        "Manual", "Draft", "user-a", null, null, null, null, null,
                        DateTime.UtcNow, new List<RequisitionLineDto>())
                }));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.GetAsync("/api/procurement/requisitions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.Should().HaveCount(1);
    }

    [Fact]
    public async Task PostReceiveInvoice_InvalidAmounts_Returns422()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<Application.Commands.ReceiveInvoice.ReceiveInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Invalid(new List<ValidationError>
            {
                new ValidationError("LineNetAmount does not equal round(Qty x UnitPrice, 4).")
            }));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediatorMock.Object);
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization(opts =>
            opts.AddPolicy("ManufacturerOnly", p => p.RequireAuthenticatedUser()));
        builder.Services.AddRouting();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInvoiceEndpoints();
        await app.StartAsync();
        var testServer = app.Services.GetRequiredService<IServer>() as TestServer;
        var client = testServer!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var lineNet = Math.Round(10 * 999m, 4); // intentionally wrong amount
        var lineVat = 0m;
        var payload = new
        {
            SupplierId = Guid.NewGuid(),
            PurchaseOrderId = Guid.NewGuid(),
            SupplierInvoiceNumber = "INV-BAD",
            InvoiceDate = "2026-05-01",
            DueDate = (string?)null,
            Currency = "HUF",
            Lines = new[]
            {
                new { MaterialCode = "WD-001", PurchaseOrderLineId = (Guid?)null, Quantity = 10, UnitPrice = 100m, LineNetAmount = lineNet, LineVatAmount = lineVat }
            }
        };

        var response = await client.PostAsJsonAsync("/api/procurement/invoices", payload);

        // ResultToHttp maps Invalid → 422 UnprocessableEntity
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
