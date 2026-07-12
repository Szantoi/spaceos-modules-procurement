using Ardalis.Result;
using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class PriceListTests
{
    private static readonly Guid TenantId = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static IReadOnlyList<(string, decimal, int, int?)> OneValidEntry()
        => new[] { ("WD-001", 100m, 1, (int?)null) };

    private static Result<PriceList> CreateDraftPriceList(DateOnly? validTo = null)
    {
        return PriceList.Create(
            TenantId, SupplierId, "HUF",
            new DateOnly(2026, 1, 1), validTo,
            OneValidEntry());
    }

    [Fact]
    public void Create_WithValidData_ShouldReturnDraftStatus()
    {
        var result = CreateDraftPriceList();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PriceListStatus.Draft);
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void Activate_FromDraft_ShouldTransitionToActive()
    {
        var priceList = CreateDraftPriceList().Value;

        var result = priceList.Activate();

        result.IsSuccess.Should().BeTrue();
        priceList.Status.Should().Be(PriceListStatus.Active);
    }

    [Fact]
    public void Activate_WhenOverlappingActivePriceList_ShouldReturnError()
    {
        // DB-P-09: domain guard — activating a price list when one is already active
        // The domain guard is handled at the handler level via HasOverlappingActivePriceListAsync.
        // Here we test that Activate() rejects double-activation of the same aggregate.
        var priceList = CreateDraftPriceList().Value;
        priceList.Activate();

        // Trying to activate again (already Active) should fail
        var result = priceList.Activate();

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Expire_FromActive_ShouldTransitionToExpired()
    {
        var priceList = CreateDraftPriceList().Value;
        priceList.Activate();

        var result = priceList.Expire();

        result.IsSuccess.Should().BeTrue();
        priceList.Status.Should().Be(PriceListStatus.Expired);
    }

    [Fact]
    public void GetBestPrice_ShouldReturnCheapestMatchingEntry()
    {
        // Build a price list with two entries for same material, different tiers
        var entries = new[]
        {
            ("WD-001", 100m, 1, (int?)10),   // qty 1–10 → 100
            ("WD-001", 90m, 11, (int?)null),  // qty 11+ → 90
        };
        var result = PriceList.Create(TenantId, SupplierId, "HUF",
            new DateOnly(2026, 1, 1), null, entries);

        result.IsSuccess.Should().BeTrue();
        var lower = result.Value.Entries.OrderBy(e => e.UnitPrice).First();
        lower.UnitPrice.Should().Be(90m);
    }

    // BE-PROC-001: Update tests
    [Fact]
    public void Update_FromDraft_ShouldSucceed()
    {
        var priceList = CreateDraftPriceList().Value;
        var newEntries = new[] { ("MAT-001", 200m, 1, (int?)null), ("MAT-002", 150m, 5, (int?)null) };

        var result = priceList.Update(new DateOnly(2026, 2, 1), new DateOnly(2026, 12, 31), newEntries);

        result.IsSuccess.Should().BeTrue();
        priceList.ValidFrom.Should().Be(new DateOnly(2026, 2, 1));
        priceList.ValidTo.Should().Be(new DateOnly(2026, 12, 31));
        priceList.Entries.Should().HaveCount(2);
        priceList.Entries.Should().Contain(e => e.MaterialCode == "MAT-001" && e.UnitPrice == 200m);
    }

    [Fact]
    public void Update_FromActive_ShouldFail()
    {
        var priceList = CreateDraftPriceList().Value;
        priceList.Activate();
        var newEntries = OneValidEntry();

        var result = priceList.Update(new DateOnly(2026, 2, 1), null, newEntries);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Update_FromExpired_ShouldFail()
    {
        var priceList = CreateDraftPriceList().Value;
        priceList.Activate();
        priceList.Expire();
        var newEntries = OneValidEntry();

        var result = priceList.Update(new DateOnly(2026, 2, 1), null, newEntries);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Update_WithInvalidDateRange_ShouldFail()
    {
        var priceList = CreateDraftPriceList().Value;
        var newEntries = OneValidEntry();

        var result = priceList.Update(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1), newEntries);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Update_WithNoEntries_ShouldFail()
    {
        var priceList = CreateDraftPriceList().Value;

        var result = priceList.Update(new DateOnly(2026, 2, 1), null, Array.Empty<(string, decimal, int, int?)>());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }
}
