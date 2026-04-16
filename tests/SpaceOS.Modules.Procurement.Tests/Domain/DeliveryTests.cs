using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class DeliveryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public void Record_WithValidData_ShouldCreateDelivery()
    {
        var delivery = Delivery.Record(TenantId, OrderId, 50m, DateTime.UtcNow, "No issues", "operator1");
        delivery.Id.Should().NotBeEmpty();
        delivery.ReceivedQuantity.Should().Be(50m);
        delivery.RecordedBy.Should().Be("operator1");
    }

    [Fact]
    public void Record_WithZeroQuantity_ShouldThrow()
    {
        var act = () => Delivery.Record(TenantId, OrderId, 0m, DateTime.UtcNow, null, "op");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NoPublicSetters_OnDelivery()
    {
        typeof(Delivery).GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true)
            .Should().BeEmpty("Delivery must have no public setters (append-only)");
    }

    [Fact]
    public void Record_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Delivery.Record(Guid.Empty, OrderId, 10m, DateTime.UtcNow, null, "op");
        act.Should().Throw<ArgumentException>();
    }
}
