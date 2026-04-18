using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Repositories;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Infrastructure;

public class ProcurementRepositoryTests : IDisposable
{
    private readonly ProcurementDbContext _db;
    private readonly ProcurementRepository _repo;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _supplierId;

    public ProcurementRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ProcurementDbContext(options);
        _repo = new ProcurementRepository(_db);

        var supplier = Supplier.Create(_tenantId, "Test Supplier", "test@supplier.com", "", "", 7, 4.0m);
        _supplierId = supplier.Id;
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();
    }

    [Fact]
    public async Task AddSupplier_ShouldPersist()
    {
        var supplier = Supplier.Create(_tenantId, "New Supplier", "new@supplier.com", "", "", 3, 3.5m);
        await _repo.AddSupplierAsync(supplier);
        await _repo.SaveChangesAsync();

        var found = await _repo.GetSupplierByIdAsync(supplier.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("New Supplier");
    }

    [Fact]
    public async Task GetActiveSuppliers_ShouldReturnOnlyActive()
    {
        var inactive = Supplier.Create(_tenantId, "Inactive", "inactive@s.com", "", "", 10, 2m);
        inactive.Deactivate();
        await _repo.AddSupplierAsync(inactive);
        await _repo.SaveChangesAsync();

        var active = await _repo.GetActiveSuppliersByTenantAsync(_tenantId);
        active.Should().NotContain(s => s.Id == inactive.Id);
        active.Should().Contain(s => s.Id == _supplierId);
    }

    [Fact]
    public async Task AddPurchaseOrder_ShouldPersist()
    {
        var order = PurchaseOrder.Create(_tenantId, _supplierId, "MDF 18mm", 50m, 4000m, "HUF", null);
        order.Submit();
        order.PopDomainEvents();

        await _repo.AddPurchaseOrderAsync(order);
        await _repo.SaveChangesAsync();

        var found = await _repo.GetPurchaseOrderByIdAsync(order.Id);
        found.Should().NotBeNull();
        found!.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    [Fact]
    public async Task GetOrdersByTenant_ShouldFilterByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var supplierA = Supplier.Create(tenantA, "SA", "sa@s.com", "", "", 1, 5m);
        var supplierB = Supplier.Create(tenantB, "SB", "sb@s.com", "", "", 1, 5m);
        await _repo.AddSupplierAsync(supplierA);
        await _repo.AddSupplierAsync(supplierB);

        var orderA = PurchaseOrder.Create(tenantA, supplierA.Id, "MDF", 10m, 1000m, "HUF", null);
        var orderB = PurchaseOrder.Create(tenantB, supplierB.Id, "MDF", 20m, 1000m, "HUF", null);
        await _repo.AddPurchaseOrderAsync(orderA);
        await _repo.AddPurchaseOrderAsync(orderB);
        await _repo.SaveChangesAsync();

        var orders = await _repo.GetOrdersByTenantAsync(tenantA);
        orders.Should().ContainSingle(o => o.Id == orderA.Id);
        orders.Should().NotContain(o => o.Id == orderB.Id);
    }

    [Fact]
    public async Task AddDelivery_ShouldPersist()
    {
        var delivery = Delivery.Record(_tenantId, Guid.NewGuid(), 30m, DateTime.UtcNow, null, "warehouse_op");
        await _repo.AddDeliveryAsync(delivery);
        await _repo.SaveChangesAsync();

        var found = await _db.Deliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == delivery.Id);
        found.Should().NotBeNull();
        found!.ReceivedQuantity.Should().Be(30m);
    }

    [Fact]
    public async Task PurchaseOrder_StatusTransitions_ShouldPersist()
    {
        var order = PurchaseOrder.Create(_tenantId, _supplierId, "HDF 3mm", 20m, 2000m, "HUF", null);
        order.Submit();
        order.PopDomainEvents();
        await _repo.AddPurchaseOrderAsync(order);
        await _repo.SaveChangesAsync();

        var tracked = await _db.PurchaseOrders.FirstAsync(o => o.Id == order.Id);
        tracked.Confirm();
        tracked.MarkShipped();
        tracked.RecordDelivery(18m);
        tracked.PopDomainEvents();
        await _repo.SaveChangesAsync();

        var found = await _repo.GetPurchaseOrderByIdAsync(order.Id);
        found!.Status.Should().Be(PurchaseOrderStatus.Delivered);
    }

    [Fact]
    public void ProcurementDbContext_ShouldUseCorrectSchema()
    {
        var schema = _db.Model.GetDefaultSchema();
        schema.Should().Be("spaceos_procurement");
    }

    [Fact]
    public async Task GetSupplierById_NonExisting_ShouldReturnNull()
    {
        var found = await _repo.GetSupplierByIdAsync(Guid.NewGuid());
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetPurchaseOrderById_NonExisting_ShouldReturnNull()
    {
        var found = await _repo.GetPurchaseOrderByIdAsync(Guid.NewGuid());
        found.Should().BeNull();
    }

    [Fact]
    public void Delivery_HasNoPublicSetters()
    {
        typeof(Delivery).GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task TenantIsolation_ShouldSeparateData()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var sA = Supplier.Create(tenantA, "SA2", "sa2@s.com", "", "", 5, 4m);
        var sB = Supplier.Create(tenantB, "SB2", "sb2@s.com", "", "", 5, 4m);
        await _repo.AddSupplierAsync(sA);
        await _repo.AddSupplierAsync(sB);
        await _repo.SaveChangesAsync();

        var suppliersA = await _db.Suppliers.AsNoTracking().Where(s => s.TenantId == tenantA).ToListAsync();
        var suppliersB = await _db.Suppliers.AsNoTracking().Where(s => s.TenantId == tenantB).ToListAsync();

        suppliersA.Should().NotContain(s => s.TenantId == tenantB);
        suppliersB.Should().NotContain(s => s.TenantId == tenantA);
    }

    public void Dispose() => _db.Dispose();
}
