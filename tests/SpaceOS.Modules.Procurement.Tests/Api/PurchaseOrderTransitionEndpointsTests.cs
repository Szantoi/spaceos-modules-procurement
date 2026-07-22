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
using SpaceOS.Modules.Procurement.Application.Commands.CancelPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.ConfirmPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.MarkPurchaseOrderShipped;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Commands.SubmitPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Api;

/// <summary>
/// WORLDS-PROC-PO-FSM: TestServer coverage for the five PurchaseOrder FSM-transition
/// endpoints (submit/confirm/ship/deliver/cancel) under /api/procurement/orders/{id}/...
/// Mirrors the mocked-mediator TestServer pattern already used by ProcurementEndpointsTests.
/// </summary>
public class PurchaseOrderTransitionEndpointsTests
{
    private static readonly Guid OrderId = Guid.NewGuid();

    private static OrderStatusResponse SampleDto(string status) => new(
        OrderId, TestAuthHandler.TestTenantId, Guid.NewGuid(), "MDF 18mm", 100m, 5000m, "HUF",
        status, null, DateTime.UtcNow);

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
        app.MapProcurementEndpoints();
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
        app.MapProcurementEndpoints();
        app.StartAsync().GetAwaiter().GetResult();

        return (app.Services.GetRequiredService<IServer>() as TestServer)!.CreateClient();
    }

    public static IEnumerable<object[]> TransitionRoutes => new List<object[]>
    {
        new object[] { "submit" },
        new object[] { "confirm" },
        new object[] { "ship" },
        new object[] { "deliver" },
        new object[] { "cancel" },
    };

    // --- 400: malformed guid — mediator must never be invoked --------------

    [Theory]
    [MemberData(nameof(TransitionRoutes))]
    public async Task Transition_MalformedGuid_Returns400(string route)
    {
        // A strict mediator mock (no .Setup at all) would throw on any unexpected Send(...)
        // call, so a passing test here proves the handler was never reached — the malformed
        // id is rejected by the manual Guid.TryParse guard before mediator.Send is invoked.
        var mediatorMock = new Mock<IMediator>(MockBehavior.Strict);
        var client = CreateAuthClient(mediatorMock);

        var response = await client.PostAsJsonAsync($"/api/procurement/orders/not-a-guid/{route}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- 401: no auth --------------------------------------------------------

    [Theory]
    [MemberData(nameof(TransitionRoutes))]
    public async Task Transition_WithoutAuth_Returns401(string route)
    {
        var mediatorMock = new Mock<IMediator>();
        var client = CreateNoAuthClient(mediatorMock);

        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/{route}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Submit --------------------------------------------------------------

    [Fact]
    public async Task Submit_Success_Returns200WithFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<SubmitPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Success(SampleDto("Submitted")));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/submit", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        dto!.Status.Should().Be("Submitted");
        dto.Id.Should().Be(OrderId);
    }

    [Fact]
    public async Task Submit_UnknownOrder_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<SubmitPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.NotFound("not found"));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/submit", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_IllegalState_Returns409()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<SubmitPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Conflict("Cannot submit order in status Confirmed."));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/submit", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- Confirm ---------------------------------------------------------

    [Fact]
    public async Task Confirm_Success_Returns200WithFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ConfirmPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Success(SampleDto("Confirmed")));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/confirm", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        dto!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Confirm_IllegalState_Returns409()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ConfirmPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Conflict("Cannot confirm order in status Draft."));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/confirm", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Confirm_UnknownOrder_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<ConfirmPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.NotFound("not found"));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/confirm", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Ship ------------------------------------------------------------

    [Fact]
    public async Task Ship_Success_Returns200WithFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<MarkPurchaseOrderShippedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Success(SampleDto("Shipped")));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/ship", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        dto!.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task Ship_IllegalState_Returns409()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<MarkPurchaseOrderShippedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Conflict("Cannot mark order as shipped in status Draft."));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/ship", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ship_UnknownOrder_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<MarkPurchaseOrderShippedCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.NotFound("not found"));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/ship", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Cancel ----------------------------------------------------------

    [Fact]
    public async Task Cancel_Success_Returns200WithFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<CancelPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Success(SampleDto("Cancelled")));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/cancel", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        dto!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Cancel_AlreadyDelivered_Returns409()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<CancelPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Conflict("Cannot cancel a delivered order."));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/cancel", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancel_UnknownOrder_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<CancelPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.NotFound("not found"));

        var client = CreateAuthClient(mediatorMock);
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/cancel", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Deliver (reuse of RecordDeliveryCommand, plus a fresh-DTO follow-up query) ---

    [Fact]
    public async Task Deliver_Success_Returns200WithFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RecordDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mediatorMock.Setup(m => m.Send(It.IsAny<GetOrderStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderStatusResponse>.Success(SampleDto("Delivered")));

        var client = CreateAuthClient(mediatorMock);
        var payload = new { ReceivedQuantity = 100m, Notes = "ok", RecordedBy = "warehouse-op" };
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/deliver", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        dto!.Status.Should().Be("Delivered");

        mediatorMock.Verify(m => m.Send(
            It.Is<RecordDeliveryCommand>(c => c.PurchaseOrderId == OrderId && c.ReceivedQuantity == 100m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deliver_IllegalState_Returns409_AndDoesNotQueryFreshDto()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RecordDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict("Cannot record delivery in status Draft."));

        var client = CreateAuthClient(mediatorMock);
        var payload = new { ReceivedQuantity = 100m, Notes = (string?)null, RecordedBy = "op" };
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/deliver", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        mediatorMock.Verify(m => m.Send(It.IsAny<GetOrderStatusQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deliver_UnknownOrder_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        mediatorMock.Setup(m => m.Send(It.IsAny<RecordDeliveryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound("not found"));

        var client = CreateAuthClient(mediatorMock);
        var payload = new { ReceivedQuantity = 100m, Notes = (string?)null, RecordedBy = "op" };
        var response = await client.PostAsJsonAsync($"/api/procurement/orders/{OrderId}/deliver", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
