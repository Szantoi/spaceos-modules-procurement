using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class PurchaseOrderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static PurchaseOrder CreateOrder(PurchaseOrderStatus? advanceTo = null)
    {
        var order = PurchaseOrder.Create(TenantId, SupplierId, "MDF 18mm", 100m, 5000m, "HUF", null);
        if (advanceTo >= PurchaseOrderStatus.Submitted) { order.Submit(); order.PopDomainEvents(); }
        if (advanceTo >= PurchaseOrderStatus.Confirmed) order.Confirm();
        if (advanceTo >= PurchaseOrderStatus.Shipped) order.MarkShipped();
        return order;
    }

    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var order = PurchaseOrder.Create(TenantId, SupplierId, "MDF 18mm", 100m, 5000m, "HUF", null);
        order.Status.Should().Be(PurchaseOrderStatus.Draft);
        order.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Submit_FromDraft_ShouldTransitionToSubmitted()
    {
        var order = CreateOrder();
        order.Submit();
        order.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    [Fact]
    public void Submit_ShouldRaisePurchaseOrderSubmittedEvent()
    {
        var order = CreateOrder();
        order.Submit();
        order.DomainEvents.Should().ContainSingle(e => e is PurchaseOrderSubmittedEvent);
    }

    [Fact]
    public void Confirm_FromSubmitted_ShouldTransitionToConfirmed()
    {
        var order = CreateOrder(PurchaseOrderStatus.Submitted);
        order.Confirm();
        order.Status.Should().Be(PurchaseOrderStatus.Confirmed);
    }

    [Fact]
    public void MarkShipped_FromConfirmed_ShouldTransitionToShipped()
    {
        var order = CreateOrder(PurchaseOrderStatus.Confirmed);
        order.MarkShipped();
        order.Status.Should().Be(PurchaseOrderStatus.Shipped);
    }

    [Fact]
    public void RecordDelivery_FromShipped_ShouldTransitionToDelivered()
    {
        var order = CreateOrder(PurchaseOrderStatus.Shipped);
        order.RecordDelivery(95m);
        order.Status.Should().Be(PurchaseOrderStatus.Delivered);
    }

    [Fact]
    public void RecordDelivery_ShouldRaiseDeliveredAndReorderEvents()
    {
        var order = CreateOrder(PurchaseOrderStatus.Shipped);
        order.RecordDelivery(95m);
        order.DomainEvents.Should().Contain(e => e is PurchaseOrderDeliveredEvent);
        order.DomainEvents.Should().Contain(e => e is ReorderAlertTriggeredEvent);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();
        order.Cancel();
        order.Status.Should().Be(PurchaseOrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDelivered_ShouldThrow()
    {
        var order = CreateOrder(PurchaseOrderStatus.Shipped);
        order.RecordDelivery(100m);
        var act = () => order.Cancel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Submit_FromConfirmed_ShouldThrow()
    {
        var order = CreateOrder(PurchaseOrderStatus.Confirmed);
        var act = () => order.Submit();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldThrow()
    {
        var act = () => PurchaseOrder.Create(TenantId, SupplierId, "MDF 18mm", 0m, 5000m, "HUF", null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NoPublicSetters_OnPurchaseOrder()
    {
        typeof(PurchaseOrder).GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true)
            .Should().BeEmpty("PurchaseOrder must have no public setters");
    }
}
