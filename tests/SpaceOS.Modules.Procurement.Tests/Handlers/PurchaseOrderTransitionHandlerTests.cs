using Ardalis.Result;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SpaceOS.Modules.Procurement.Application.Commands.CancelPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.ConfirmPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.MarkPurchaseOrderShipped;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Commands.SubmitPurchaseOrder;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Handlers;

/// <summary>
/// WORLDS-PROC-PO-FSM: handler-level coverage for the Submit/Confirm/Ship/Cancel
/// transition commands, plus the Deliver reuse-path (RecordDeliveryCommandHandler)
/// now that a Shipped order can arrive pre-shipped via MarkPurchaseOrderShippedCommand.
/// </summary>
public class PurchaseOrderTransitionHandlerTests
{
    private static readonly Guid TenantId = new("60000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = new("60000000-0000-0000-0000-000000000002");

    private static ProcurementDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase($"po-transition-handler-test-{Guid.NewGuid()}")
            .Options;
        return new ProcurementDbContext(options);
    }

    private static PurchaseOrder SeedOrder(ProcurementDbContext db, Guid tenantId, PurchaseOrderStatus target)
    {
        var order = PurchaseOrder.Create(tenantId, Guid.NewGuid(), "MDF 18mm", 100m, 5000m, "HUF", null);
        if (target >= PurchaseOrderStatus.Submitted) { order.Submit(); order.PopDomainEvents(); }
        if (target >= PurchaseOrderStatus.Confirmed) order.Confirm();
        if (target >= PurchaseOrderStatus.Shipped) order.MarkShipped();
        if (target == PurchaseOrderStatus.Delivered) { order.RecordDelivery(100m); order.PopDomainEvents(); }
        if (target == PurchaseOrderStatus.Cancelled) order.Cancel();

        db.PurchaseOrders.Add(order);
        db.SaveChanges();
        return order;
    }

    // --- Submit ---------------------------------------------------------

    [Fact]
    public async Task Submit_FromDraft_ShouldSucceedAndReturnFreshDto()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Draft);
        var handler = new SubmitPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Submitted");
        result.Value.Id.Should().Be(order.Id);

        (await db.PurchaseOrders.FindAsync(order.Id))!.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    [Fact]
    public async Task Submit_FromConfirmed_ShouldReturnConflict()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Confirmed);
        var handler = new SubmitPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Submit_UnknownId_ShouldReturnNotFound()
    {
        await using var db = CreateInMemoryDb();
        var handler = new SubmitPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Submit_OrderBelongsToOtherTenant_ShouldReturnNotFound()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, OtherTenantId, PurchaseOrderStatus.Draft);
        var handler = new SubmitPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Submit_CalledTwice_SecondCallIsConflict_NoDuplicateSideEffect()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Draft);
        var handler = new SubmitPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var first = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);
        var second = await handler.Handle(new SubmitPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Status.Should().Be(ResultStatus.Conflict);
        (await db.PurchaseOrders.FindAsync(order.Id))!.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    // --- Confirm ----------------------------------------------------------

    [Fact]
    public async Task Confirm_FromSubmitted_ShouldSucceed()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Submitted);
        var handler = new ConfirmPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new ConfirmPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Confirm_FromDraft_ShouldReturnConflict()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Draft);
        var handler = new ConfirmPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new ConfirmPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    // --- MarkShipped --------------------------------------------------------

    [Fact]
    public async Task Ship_FromConfirmed_ShouldSucceed()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Confirmed);
        var handler = new MarkPurchaseOrderShippedCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new MarkPurchaseOrderShippedCommand(TenantId, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task Ship_FromSubmitted_ShouldReturnConflict()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Submitted);
        var handler = new MarkPurchaseOrderShippedCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new MarkPurchaseOrderShippedCommand(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    // --- Cancel -------------------------------------------------------------

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.Submitted)]
    [InlineData(PurchaseOrderStatus.Confirmed)]
    [InlineData(PurchaseOrderStatus.Shipped)]
    public async Task Cancel_FromNonTerminalStates_ShouldSucceed(PurchaseOrderStatus from)
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, from);
        var handler = new CancelPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new CancelPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Cancel_FromDelivered_ShouldReturnConflict()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Delivered);
        var handler = new CancelPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var result = await handler.Handle(new CancelPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task Cancel_CalledTwice_SecondCallIsConflict()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Draft);
        var handler = new CancelPurchaseOrderCommandHandler(new ProcurementRepository(db));

        var first = await handler.Handle(new CancelPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);
        var second = await handler.Handle(new CancelPurchaseOrderCommand(TenantId, order.Id), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Status.Should().Be(ResultStatus.Conflict);
    }

    // --- Deliver reuse path: Ship endpoint + existing RecordDelivery handler ------

    private static Mock<SpaceOS.Modules.Inventory.Contracts.Providers.IInventoryProvider> NoopInventoryMock()
    {
        var mock = new Mock<SpaceOS.Modules.Inventory.Contracts.Providers.IInventoryProvider>();
        mock.Setup(x => x.RecordInboundAsync(
                It.IsAny<SpaceOS.Modules.Inventory.Contracts.Dtos.StockMovementDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task Deliver_AfterExplicitShipEndpoint_ShouldStillSucceed()
    {
        // WORLDS-PROC-PO-FSM gap fix: RecordDeliveryCommandHandler used to unconditionally
        // call MarkShipped() internally, which would throw once a dedicated ship endpoint
        // had already moved the order to Shipped. Guard added: only ship if still Confirmed.
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Confirmed);

        var repo = new ProcurementRepository(db);
        var v2Repo = new ProcurementV2Repository(db);
        var shipHandler = new MarkPurchaseOrderShippedCommandHandler(repo);
        var shipResult = await shipHandler.Handle(new MarkPurchaseOrderShippedCommand(TenantId, order.Id), CancellationToken.None);
        shipResult.IsSuccess.Should().BeTrue("the order must be persisted as Shipped before delivery");

        var deliverHandler = new RecordDeliveryCommandHandler(repo, v2Repo, NoopInventoryMock().Object);
        var deliverResult = await deliverHandler.Handle(
            new RecordDeliveryCommand(TenantId, order.Id, 100m, null, "warehouse-op"), CancellationToken.None);

        deliverResult.IsSuccess.Should().BeTrue();
        (await db.PurchaseOrders.FindAsync(order.Id))!.Status.Should().Be(PurchaseOrderStatus.Delivered);
        (await db.Deliveries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Deliver_CalledTwice_SecondCallIsConflict_NoDuplicateOutboxOrDelivery()
    {
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Shipped);

        var repo = new ProcurementRepository(db);
        var v2Repo = new ProcurementV2Repository(db);
        var handler = new RecordDeliveryCommandHandler(repo, v2Repo, NoopInventoryMock().Object);

        var first = await handler.Handle(new RecordDeliveryCommand(TenantId, order.Id, 100m, null, "op"), CancellationToken.None);
        var second = await handler.Handle(new RecordDeliveryCommand(TenantId, order.Id, 100m, null, "op"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Status.Should().Be(ResultStatus.Conflict);

        (await db.Deliveries.CountAsync()).Should().Be(1, "a repeated delivery request must not create a second delivery row");
        (await db.OutboxMessages.CountAsync()).Should().Be(1, "a repeated delivery request must not create a second inventory-inbound outbox row");
    }

    [Fact]
    public async Task Deliver_FromDraft_ShouldReturnConflictNotThrow()
    {
        // Previously this call would throw an unhandled InvalidOperationException from
        // MarkShipped() (Cannot mark order as shipped in status Draft) instead of a
        // graceful 409 — the try/catch fix in RecordDeliveryCommandHandler covers this too.
        await using var db = CreateInMemoryDb();
        var order = SeedOrder(db, TenantId, PurchaseOrderStatus.Draft);

        var repo = new ProcurementRepository(db);
        var v2Repo = new ProcurementV2Repository(db);
        var handler = new RecordDeliveryCommandHandler(repo, v2Repo, NoopInventoryMock().Object);

        var result = await handler.Handle(new RecordDeliveryCommand(TenantId, order.Id, 10m, null, "op"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        (await db.Deliveries.CountAsync()).Should().Be(0);
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }
}
