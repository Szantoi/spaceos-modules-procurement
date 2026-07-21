using Ardalis.Result;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Handlers;

/// <summary>
/// GetOrderStatusQueryHandler — order-detail read model. Goes through
/// IProcurementRepository (repository port), never touches the DbContext directly.
/// </summary>
public class OrderStatusHandlerTests
{
    private static readonly Guid TenantId = new("50000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = new("50000000-0000-0000-0000-000000000002");

    private static ProcurementDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase($"order-status-handler-test-{Guid.NewGuid()}")
            .Options;
        return new ProcurementDbContext(options);
    }

    [Fact]
    public async Task Handle_ExistingOrderInTenant_ShouldReturnRealFields()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementRepository(db);
        var supplierId = Guid.NewGuid();
        var order = PurchaseOrder.Create(TenantId, supplierId, "MDF 18mm", 50m, 4000m, "HUF", null);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetOrderStatusQueryHandler(repo);
        var result = await handler.Handle(new GetOrderStatusQuery(TenantId, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(order.Id);
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.SupplierId.Should().Be(supplierId);
        result.Value.MaterialType.Should().Be("MDF 18mm");
        result.Value.Quantity.Should().Be(50m);
        result.Value.UnitPrice.Should().Be(4000m);
        result.Value.Currency.Should().Be("HUF");
        result.Value.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_UnknownId_ShouldReturnNotFound()
    {
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementRepository(db);
        var handler = new GetOrderStatusQueryHandler(repo);

        var result = await handler.Handle(new GetOrderStatusQuery(TenantId, Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_OrderBelongsToOtherTenant_ShouldReturnNotFound()
    {
        // Tenant isolation: an order that exists but under a different tenant must
        // not be distinguishable from "does not exist" (no cross-tenant existence leak).
        await using var db = CreateInMemoryDb();
        var repo = new ProcurementRepository(db);
        var order = PurchaseOrder.Create(OtherTenantId, Guid.NewGuid(), "MDF 18mm", 50m, 4000m, "HUF", null);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();

        var handler = new GetOrderStatusQueryHandler(repo);
        var result = await handler.Handle(new GetOrderStatusQuery(TenantId, order.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
