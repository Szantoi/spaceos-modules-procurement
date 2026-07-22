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

    // ---------------------------------------------------------------------
    // WORLDS-PROC-PO-FSM: full legal/illegal transition matrix.
    // PurchaseOrder is the ONLY source of truth for these rules — this test
    // asserts the aggregate's own guard behaviour, it does not re-derive it.
    // ---------------------------------------------------------------------

    public enum PoAction { Submit, Confirm, Ship, Deliver, Cancel }

    /// <summary>Builds an order already sitting in <paramref name="target"/> status.</summary>
    private static PurchaseOrder CreateOrderAt(PurchaseOrderStatus target)
    {
        var order = PurchaseOrder.Create(TenantId, SupplierId, "MDF 18mm", 100m, 5000m, "HUF", null);
        if (target == PurchaseOrderStatus.Draft) return order;

        order.Submit();
        order.PopDomainEvents();
        if (target == PurchaseOrderStatus.Submitted) return order;

        order.Confirm();
        if (target == PurchaseOrderStatus.Confirmed) return order;

        order.MarkShipped();
        if (target == PurchaseOrderStatus.Shipped) return order;

        if (target == PurchaseOrderStatus.Delivered)
        {
            order.RecordDelivery(100m);
            order.PopDomainEvents();
            return order;
        }

        if (target == PurchaseOrderStatus.Cancelled)
        {
            // Cancel is legal from Shipped too; use it to reach the terminal Cancelled state.
            order.Cancel();
            return order;
        }

        throw new ArgumentOutOfRangeException(nameof(target), target, "Unhandled target status in test helper.");
    }

    private static void InvokeAction(PurchaseOrder order, PoAction action)
    {
        switch (action)
        {
            case PoAction.Submit: order.Submit(); break;
            case PoAction.Confirm: order.Confirm(); break;
            case PoAction.Ship: order.MarkShipped(); break;
            case PoAction.Deliver: order.RecordDelivery(10m); break;
            case PoAction.Cancel: order.Cancel(); break;
            default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    // from-state | action | expected: transition succeeds (true) or throws InvalidOperationException (false)
    [Theory]
    [InlineData(PurchaseOrderStatus.Draft, PoAction.Submit, true)]
    [InlineData(PurchaseOrderStatus.Draft, PoAction.Confirm, false)]
    [InlineData(PurchaseOrderStatus.Draft, PoAction.Ship, false)]
    [InlineData(PurchaseOrderStatus.Draft, PoAction.Deliver, false)]
    [InlineData(PurchaseOrderStatus.Draft, PoAction.Cancel, true)]
    [InlineData(PurchaseOrderStatus.Submitted, PoAction.Submit, false)]
    [InlineData(PurchaseOrderStatus.Submitted, PoAction.Confirm, true)]
    [InlineData(PurchaseOrderStatus.Submitted, PoAction.Ship, false)]
    [InlineData(PurchaseOrderStatus.Submitted, PoAction.Deliver, false)]
    [InlineData(PurchaseOrderStatus.Submitted, PoAction.Cancel, true)]
    [InlineData(PurchaseOrderStatus.Confirmed, PoAction.Submit, false)]
    [InlineData(PurchaseOrderStatus.Confirmed, PoAction.Confirm, false)]
    [InlineData(PurchaseOrderStatus.Confirmed, PoAction.Ship, true)]
    [InlineData(PurchaseOrderStatus.Confirmed, PoAction.Deliver, false)]
    [InlineData(PurchaseOrderStatus.Confirmed, PoAction.Cancel, true)]
    [InlineData(PurchaseOrderStatus.Shipped, PoAction.Submit, false)]
    [InlineData(PurchaseOrderStatus.Shipped, PoAction.Confirm, false)]
    [InlineData(PurchaseOrderStatus.Shipped, PoAction.Ship, false)]
    [InlineData(PurchaseOrderStatus.Shipped, PoAction.Deliver, true)]
    [InlineData(PurchaseOrderStatus.Shipped, PoAction.Cancel, true)]
    [InlineData(PurchaseOrderStatus.Delivered, PoAction.Submit, false)]
    [InlineData(PurchaseOrderStatus.Delivered, PoAction.Confirm, false)]
    [InlineData(PurchaseOrderStatus.Delivered, PoAction.Ship, false)]
    [InlineData(PurchaseOrderStatus.Delivered, PoAction.Deliver, false)]
    [InlineData(PurchaseOrderStatus.Delivered, PoAction.Cancel, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, PoAction.Submit, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, PoAction.Confirm, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, PoAction.Ship, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, PoAction.Deliver, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, PoAction.Cancel, false)]
    public void TransitionMatrix_ShouldMatchAggregateGuards(PurchaseOrderStatus from, PoAction action, bool expectSuccess)
    {
        var order = CreateOrderAt(from);

        Action act = () => InvokeAction(order, action);

        if (expectSuccess)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<InvalidOperationException>();
            order.Status.Should().Be(from, "an illegal transition must not mutate the aggregate's state");
        }
    }

    [Fact]
    public void Submit_CalledTwice_SecondCallThrows_AndDoesNotRaiseSecondEvent()
    {
        // Idempotency-guard: repeating the exact same transition must not produce
        // a duplicate domain event — this is the mechanism new HTTP endpoints rely on.
        var order = CreateOrder();
        order.Submit();
        order.PopDomainEvents();

        var act = () => order.Submit();

        act.Should().Throw<InvalidOperationException>();
        order.DomainEvents.Should().BeEmpty("the second, rejected Submit() must not raise a second event");
        order.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    [Fact]
    public void RecordDelivery_CalledTwice_SecondCallThrows_AndDoesNotRaiseSecondDeliveredEvent()
    {
        var order = CreateOrder(PurchaseOrderStatus.Shipped);
        order.RecordDelivery(95m);
        order.PopDomainEvents();

        var act = () => order.RecordDelivery(95m);

        act.Should().Throw<InvalidOperationException>();
        order.DomainEvents.Should().BeEmpty("the second, rejected RecordDelivery() must not raise duplicate events");
        order.Status.Should().Be(PurchaseOrderStatus.Delivered);
    }
}
