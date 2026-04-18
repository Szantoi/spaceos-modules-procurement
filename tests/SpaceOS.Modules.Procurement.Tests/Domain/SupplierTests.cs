using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class SupplierTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldBeActive()
    {
        var supplier = Supplier.Create(TenantId, "Acme Boards", "acme@example.com", "", "", 5, 4.5m);
        supplier.IsActive.Should().BeTrue();
        supplier.Id.Should().NotBeEmpty();
        supplier.Name.Should().Be("Acme Boards");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var supplier = Supplier.Create(TenantId, "Acme Boards", "acme@example.com", "", "", 5, 4.5m);
        supplier.Deactivate();
        supplier.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Supplier.Create(Guid.Empty, "Acme", "acme@e.com", "", "", 5, 4m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidRating_ShouldThrow()
    {
        var act = () => Supplier.Create(TenantId, "Acme", "acme@e.com", "", "", 5, 6m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NoPublicSetters_OnSupplier()
    {
        typeof(Supplier).GetProperties()
            .Where(p => p.CanWrite && p.GetSetMethod()?.IsPublic == true)
            .Should().BeEmpty("Supplier must have no public setters");
    }
}
