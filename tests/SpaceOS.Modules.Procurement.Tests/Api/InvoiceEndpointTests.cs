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
using SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoiceWithVariance;
using SpaceOS.Modules.Procurement.Application.Commands.DisputeInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.RunMatch;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Application.Queries.GetInvoices;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;
using SpaceOS.Modules.Procurement.Domain.Enums;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Api;

public class InvoiceEndpointTests
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
        app.MapInvoiceEndpoints();
        app.StartAsync().GetAwaiter().GetResult();
        var testServer = app.Services.GetRequiredService<IServer>() as TestServer;
        var client = testServer!.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        return client;
    }

    private static object ValidInvoicePayload()
    {
        var lineNet = Math.Round(10 * 100m, 4);
        var lineVat = Math.Round(lineNet * 0.27m, 2);
        return new
        {
            SupplierId = Guid.NewGuid(),
            PurchaseOrderId = Guid.NewGuid(),
            SupplierInvoiceNumber = "INV-2026-001",
            InvoiceDate = "2026-05-01",
            DueDate = (string?)null,
            Currency = "HUF",
            Lines = new[]
            {
                new { MaterialCode = "WD-001", PurchaseOrderLineId = (Guid?)null, Quantity = 10, UnitPrice = 100m, LineNetAmount = lineNet, LineVatAmount = lineVat }
            }
        };
    }

    [Fact]
    public async Task PostInvoices_WithAuth_Returns201()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ReceiveInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync("/api/procurement/invoices", ValidInvoicePayload());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostInvoicesMatch_Returns200()
    {
        var invoiceId = Guid.NewGuid();
        var matchResult = new MatchResult(
            Guid.NewGuid(),
            new List<MatchLineResult>(),
            MatchOutcome.Matched,
            "All lines matched within tolerance.");

        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RunMatchCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MatchResult>.Success(matchResult));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/invoices/{invoiceId}/match", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostInvoicesApprove_Returns200()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ApproveInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/invoices/{Guid.NewGuid()}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostInvoicesApproveWithVariance_WithoutApproverRole_Returns403()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ApproveInvoiceWithVarianceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden());

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/invoices/{Guid.NewGuid()}/approve-with-variance", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostInvoicesDispute_Returns200()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<DisputeInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/invoices/{Guid.NewGuid()}/dispute",
            new { Reason = "Price discrepancy" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInvoices_WithAuth_Returns200List()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetInvoicesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<InvoiceDto>>.Success(
                new List<InvoiceDto>
                {
                    new(Guid.NewGuid(), TestAuthHandler.TestTenantId, Guid.NewGuid(), Guid.NewGuid(),
                        "INV-001", DateOnly.FromDateTime(DateTime.Today), null, "HUF",
                        "Received", 1000m, 270m, 1270m, null,
                        "user-a", null, null, DateTime.UtcNow, new List<InvoiceLineDto>())
                }));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.GetAsync("/api/procurement/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<object>>();
        body.Should().HaveCount(1);
    }
}
