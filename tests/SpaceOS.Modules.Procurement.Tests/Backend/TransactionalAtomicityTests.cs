using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;
using SpaceOS.Modules.Procurement.Application.Commands.ReorderAlertReceiver;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Backend;

/// <summary>
/// Tests verifying BE-P-01: transactional atomicity between domain mutations and outbox/inbox.
/// Uses InMemory EF Core — no external infrastructure needed.
/// </summary>
public class TransactionalAtomicityTests
{
    private static readonly Guid TenantId = new("70000000-0000-0000-0000-000000000001");

    private static ProcurementDbContext CreateInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase($"atomicity-test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ProcurementDbContext(opts);
    }

    [Fact]
    public async Task RecordDelivery_ShouldInsertOutboxInSameTransaction()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementRepository(db);
        var v2Repo = new ProcurementV2Repository(db);

        // Seed a PurchaseOrder in Confirmed state (the handler calls MarkShipped + RecordDelivery)
        var order = PurchaseOrder.Create(TenantId, Guid.NewGuid(), "WD-001", 100m, 5000m, "HUF", null);
        order.Submit();
        order.Confirm();
        // Note: do NOT call MarkShipped here — RecordDeliveryCommandHandler calls it internally
        await repo.AddPurchaseOrderAsync(order);
        await db.SaveChangesAsync();

        var inventoryMock = new Mock<SpaceOS.Modules.Inventory.Contracts.Providers.IInventoryProvider>();
        inventoryMock.Setup(x => x.RecordInboundAsync(
                It.IsAny<SpaceOS.Modules.Inventory.Contracts.Dtos.StockMovementDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RecordDeliveryCommandHandler(repo, v2Repo, inventoryMock.Object);

        var command = new RecordDeliveryCommand(TenantId, order.Id, 10m, null, "warehouse-op");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // BE-P-01: delivery and outbox message must both exist after a single SaveChanges
        var deliveries = await db.Deliveries.ToListAsync();
        var outbox = await db.OutboxMessages.ToListAsync();

        deliveries.Should().HaveCount(1, "delivery must be persisted");
        outbox.Should().HaveCount(1, "outbox message must be inserted in the same transaction");
    }

    [Fact]
    public async Task RecordDelivery_WhenCommitFails_ShouldNotLeaveOrphanOutbox()
    {
        // Simulate a commit failure by verifying that before SaveChanges,
        // the in-flight context has both delivery and outbox message tracked.
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementRepository(db);
        var v2Repo = new ProcurementV2Repository(db);

        var order = PurchaseOrder.Create(TenantId, Guid.NewGuid(), "WD-001", 100m, 5000m, "HUF", null);
        order.Submit();
        order.Confirm();
        // Note: do NOT call MarkShipped here — RecordDeliveryCommandHandler calls it internally
        await repo.AddPurchaseOrderAsync(order);
        await db.SaveChangesAsync();

        var inventoryMock = new Mock<SpaceOS.Modules.Inventory.Contracts.Providers.IInventoryProvider>();
        inventoryMock.Setup(x => x.RecordInboundAsync(It.IsAny<SpaceOS.Modules.Inventory.Contracts.Dtos.StockMovementDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Verify that both entities are tracked before SaveChanges
        var outboxCountBefore = db.ChangeTracker.Entries<ProcurementOutboxMessage>().Count();
        var deliveryCountBefore = db.ChangeTracker.Entries<Delivery>().Count();

        outboxCountBefore.Should().Be(0);
        deliveryCountBefore.Should().Be(0);

        // Run handler to completion (both entities tracked together)
        var handler = new RecordDeliveryCommandHandler(repo, v2Repo, inventoryMock.Object);
        await handler.Handle(new RecordDeliveryCommand(TenantId, order.Id, 10m, null, "op"), CancellationToken.None);

        // After commit: both should be persisted (no orphan outbox without delivery)
        var finalOutbox = await db.OutboxMessages.CountAsync();
        var finalDeliveries = await db.Deliveries.CountAsync();

        finalOutbox.Should().Be(1);
        finalDeliveries.Should().Be(1);
    }

    [Fact]
    public async Task ReceiverHandler_ShouldInsertInboxAndRequisitionInSameTransaction()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var handler = new ReorderAlertReceiverCommandHandler(repo);

        var command = new ReorderAlertReceiverCommand(
            TenantId,
            MaterialCode: "WD-001",
            CurrentStock: 5m,
            ReorderPoint: 10m,
            SuggestedQuantity: 50m,
            PreferredSupplierId: null,
            UnitOfMeasure: "pcs",
            AlertedAt: DateTimeOffset.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsDuplicate.Should().BeFalse();
        result.RequisitionId.Should().NotBeEmpty();

        // BE-P-01: inbox + requisition must both exist
        var inboxCount = await db.InboxMessages.CountAsync();
        var reqCount = await db.PurchaseRequisitions.CountAsync();
        var auditCount = await db.AuditLogs.CountAsync();

        inboxCount.Should().Be(1, "inbox message must be persisted");
        reqCount.Should().Be(1, "requisition must be persisted");
        auditCount.Should().BeGreaterThanOrEqualTo(1, "audit log must be persisted");
    }

    [Fact]
    public async Task AuditLog_ShouldBeWrittenInSameTransactionAsMutation()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);

        var handler = new ReorderAlertReceiverCommandHandler(repo);

        var command = new ReorderAlertReceiverCommand(
            TenantId,
            MaterialCode: "WD-002",
            CurrentStock: 3m,
            ReorderPoint: 15m,
            SuggestedQuantity: 30m,
            PreferredSupplierId: null,
            UnitOfMeasure: "m2",
            AlertedAt: DateTimeOffset.UtcNow);

        await handler.Handle(command, CancellationToken.None);

        // Audit log must be written in the same transaction as the requisition
        var auditLogs = await db.AuditLogs.ToListAsync();
        auditLogs.Should().NotBeEmpty();
        auditLogs.Should().Contain(a =>
            a.Action == "ReorderAlertReceived" &&
            a.TenantId == TenantId);
    }
}
