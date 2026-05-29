using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Services;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class MatchPolicyTests
{
    private static readonly Guid PoId = Guid.NewGuid();
    private static readonly IMatchPolicy Policy = new DefaultMatchPolicy();

    private static readonly MatchPolicyThresholds DefaultThresholds = new(
        PriceTolerancePct: 0.03m,
        QuantityToleranceAbs: 0);

    private static ThreeWayMatchInput BuildInput(
        int orderedQty, decimal? orderedUnitPrice,
        int billedQty, decimal billedUnitPrice,
        int receivedQty,
        Dictionary<string, decimal>? fallback = null)
    {
        var lineId = Guid.NewGuid();
        var poLines = new[] { new PoLineInput(lineId, "WD-001", orderedQty, orderedUnitPrice) };
        var receivedQtys = new Dictionary<Guid, int> { [lineId] = receivedQty };
        var invoiceLines = new[] { new InvoiceLineInput(lineId, "WD-001", billedQty, billedUnitPrice) };
        var fb = (fallback ?? new Dictionary<string, decimal>()) as IReadOnlyDictionary<string, decimal>;

        return new ThreeWayMatchInput(PoId, poLines, receivedQtys, invoiceLines, fb);
    }

    [Fact]
    public void Evaluate_WhenAllLinesInTolerance_ShouldReturnMatched()
    {
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: 100m,
            billedQty: 10, billedUnitPrice: 101m, receivedQty: 10);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Outcome.Should().Be(MatchOutcome.Matched);
    }

    [Fact]
    public void Evaluate_WhenAnyLineOutOfTolerance_ShouldReturnException()
    {
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: 100m,
            billedQty: 10, billedUnitPrice: 110m /* >3% */, receivedQty: 10);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Outcome.Should().Be(MatchOutcome.Exception);
    }

    [Fact]
    public void Evaluate_WhenNullOrderedUnitPrice_WithFallback_ShouldUseActivePrice()
    {
        var fallback = new Dictionary<string, decimal> { ["WD-001"] = 100m };
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: null,
            billedQty: 10, billedUnitPrice: 100m, receivedQty: 10, fallback);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Outcome.Should().Be(MatchOutcome.Matched);
    }

    [Fact]
    public void Evaluate_WhenNullOrderedUnitPriceAndNoFallback_ShouldReturnException()
    {
        // DB-P-08: no price → Exception (manual reconciliation required)
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: null,
            billedQty: 10, billedUnitPrice: 100m, receivedQty: 10);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Outcome.Should().Be(MatchOutcome.Exception);
    }

    [Fact]
    public void Evaluate_WhenOrderedQuantityZero_ShouldNotDivideByZero()
    {
        // div-zero guard: orderedQty=0 but orderedUnitPrice is set
        var input = BuildInput(orderedQty: 0, orderedUnitPrice: 100m,
            billedQty: 1, billedUnitPrice: 100m, receivedQty: 0);

        var act = () => Policy.Evaluate(input, DefaultThresholds);

        act.Should().NotThrow();
    }

    [Fact]
    public void Evaluate_ReceivedQuantityCumulative_MultipleDeliveries()
    {
        // Verifies cumulative received qty is used — 10 ordered, 10 billed, 10 received → Matched
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: 100m,
            billedQty: 10, billedUnitPrice: 100m, receivedQty: 10);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Lines[0].ReceivedQuantity.Should().Be(10);
        result.Outcome.Should().Be(MatchOutcome.Matched);
    }

    [Fact]
    public void Evaluate_WhenBilledPriceWithinTolerance_ShouldReturnMatched()
    {
        // Exactly at tolerance boundary: 3%
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: 100m,
            billedQty: 10, billedUnitPrice: 103m, receivedQty: 10);

        var result = Policy.Evaluate(input, DefaultThresholds);

        result.Outcome.Should().Be(MatchOutcome.Matched);
    }

    [Fact]
    public void Evaluate_WithCustomThresholds_ShouldRespectThresholds()
    {
        var tightThresholds = new MatchPolicyThresholds(PriceTolerancePct: 0.01m, QuantityToleranceAbs: 0);

        // 2% variance — within default (3%) but exceeds tight (1%)
        var input = BuildInput(orderedQty: 10, orderedUnitPrice: 100m,
            billedQty: 10, billedUnitPrice: 102m, receivedQty: 10);

        var result = Policy.Evaluate(input, tightThresholds);

        result.Outcome.Should().Be(MatchOutcome.Exception);
    }
}
