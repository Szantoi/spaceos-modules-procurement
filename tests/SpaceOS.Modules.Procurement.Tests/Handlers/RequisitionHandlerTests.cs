using Ardalis.Result;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Application.Commands.ApprovePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.ConvertRequisitionToPurchaseOrder;
using SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;
using SpaceOS.Modules.Procurement.Application.Commands.RejectPurchaseRequisition;
using SpaceOS.Modules.Procurement.Application.Commands.RunMatch;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Services;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Handlers;

public class RequisitionHandlerTests
{
    private static readonly Guid TenantId = new("40000000-0000-0000-0000-000000000001");

    private static ProcurementDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase($"procurement-handler-test-{Guid.NewGuid()}")
            .Options;
        return new ProcurementDbContext(options);
    }

    private static IReadOnlyList<RequisitionLineRequest> OneLine()
        => new[] { new RequisitionLineRequest("WD-001", 10, null, null, null) };

    [Fact]
    public async Task CreatePurchaseRequisition_ShouldReturnRequisitionId()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var handler = new CreatePurchaseRequisitionCommandHandler(repo);

        var command = new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        db.PurchaseRequisitions.Should().HaveCount(1);
        db.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApprovePurchaseRequisition_WithoutApproverRole_ShouldReturnForbidden()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var createHandler = new CreatePurchaseRequisitionCommandHandler(repo);
        var approveHandler = new ApprovePurchaseRequisitionCommandHandler(repo);

        var createResult = await createHandler.Handle(
            new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine()),
            CancellationToken.None);

        var result = await approveHandler.Handle(
            new ApprovePurchaseRequisitionCommand(TenantId, createResult.Value, "user-b", new List<string>()),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task ApprovePurchaseRequisition_WithApproverRole_ShouldSucceed()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var createHandler = new CreatePurchaseRequisitionCommandHandler(repo);
        var approveHandler = new ApprovePurchaseRequisitionCommandHandler(repo);

        var createResult = await createHandler.Handle(
            new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine()),
            CancellationToken.None);

        var result = await approveHandler.Handle(
            new ApprovePurchaseRequisitionCommand(
                TenantId, createResult.Value, "user-b",
                new List<string> { "procurement.approver" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var req = await db.PurchaseRequisitions.FindAsync(createResult.Value);
        req!.Status.Should().Be(RequisitionStatus.Approved);
    }

    [Fact]
    public async Task ApprovePurchaseRequisition_SodViolation_ShouldReturnForbidden()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var createHandler = new CreatePurchaseRequisitionCommandHandler(repo);
        var approveHandler = new ApprovePurchaseRequisitionCommandHandler(repo);

        var createResult = await createHandler.Handle(
            new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine()),
            CancellationToken.None);

        // user-a tries to approve their own requisition (SoD violation)
        var result = await approveHandler.Handle(
            new ApprovePurchaseRequisitionCommand(
                TenantId, createResult.Value, "user-a",
                new List<string> { "procurement.approver" }),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task RejectPurchaseRequisition_ShouldSucceed()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var createHandler = new CreatePurchaseRequisitionCommandHandler(repo);
        var rejectHandler = new RejectPurchaseRequisitionCommandHandler(repo);

        var createResult = await createHandler.Handle(
            new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine()),
            CancellationToken.None);

        var result = await rejectHandler.Handle(
            new RejectPurchaseRequisitionCommand(TenantId, createResult.Value, "user-b", "Budget exceeded"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var req = await db.PurchaseRequisitions.FindAsync(createResult.Value);
        req!.Status.Should().Be(RequisitionStatus.Rejected);
    }

    [Fact]
    public async Task ConvertRequisitionToPurchaseOrder_ShouldCreatePurchaseOrder()
    {
        await using var db = CreateInMemoryDb();
        var v2Repo = new ProcurementV2Repository(db);
        var coreRepo = new ProcurementRepository(db);
        var createHandler = new CreatePurchaseRequisitionCommandHandler(v2Repo);
        var approveHandler = new ApprovePurchaseRequisitionCommandHandler(v2Repo);
        var convertHandler = new ConvertRequisitionToPurchaseOrderCommandHandler(v2Repo, coreRepo);

        var createResult = await createHandler.Handle(
            new CreatePurchaseRequisitionCommand(TenantId, "user-a", OneLine()),
            CancellationToken.None);

        await approveHandler.Handle(
            new ApprovePurchaseRequisitionCommand(TenantId, createResult.Value, "user-b",
                new List<string> { "procurement.approver" }),
            CancellationToken.None);

        var convertCommand = new ConvertRequisitionToPurchaseOrderCommand(
            TenantId, createResult.Value,
            SupplierId: Guid.NewGuid(),
            MaterialType: "WD-001",
            Quantity: 10m,
            UnitPrice: 100m,
            Currency: "HUF",
            Actor: "user-b",
            ExpectedDeliveryDate: null);

        var result = await convertHandler.Handle(convertCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        db.PurchaseOrders.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunMatch_ShouldCallMatchPolicyWithGroupByQuery()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);

        // Setup: supplier, PO, invoice in the database
        var supplierId = Guid.NewGuid();
        var order = PurchaseOrder.Create(TenantId, supplierId, "WD-001", 10m, 100m, "HUF", null);
        order.Submit();
        order.Confirm();
        order.MarkShipped();
        order.RecordDelivery(10m);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();

        var lineNet = Math.Round(10 * 100m, 4);
        var lineVat = Math.Round(lineNet * 0.27m, 2);
        var invoiceResult = SupplierInvoice.Receive(
            TenantId, supplierId, order.Id,
            "INV-RUN-01", DateOnly.FromDateTime(DateTime.Today), null,
            "HUF", "user-a",
            new[] { ("WD-001", (Guid?)null, 10, 100m, lineNet, lineVat) });
        db.SupplierInvoices.Add(invoiceResult.Value);
        await db.SaveChangesAsync();

        var handler = new RunMatchCommandHandler(repo, new DefaultMatchPolicy());
        var result = await handler.Handle(
            new RunMatchCommand(TenantId, invoiceResult.Value.Id, "user-b", new List<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ReceiveInvoice_WithInvalidAmounts_ShouldReturnInvalid()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementV2Repository(db);
        var handler = new ReceiveInvoiceCommandHandler(repo);

        // Mismatched LineNetAmount (SEC-P-07)
        var command = new ReceiveInvoiceCommand(
            TenantId, Guid.NewGuid(), Guid.NewGuid(),
            "INV-BAD", DateOnly.FromDateTime(DateTime.Today), null,
            "HUF", "user-a",
            new[] { new InvoiceLineRequest("WD-001", null, 10, 100m, 999m, 0m) });

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }
}
