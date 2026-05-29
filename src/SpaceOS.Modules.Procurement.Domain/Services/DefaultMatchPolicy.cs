using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Services;

/// <summary>
/// Default three-way match policy implementation.
/// Pure computation — no I/O. Stateless.
/// DB-P-08: handles null OrderedUnitPrice via fallback, div-zero guard.
/// OPEN-07: ReceivedQuantity is cumulative across all Delivery lines for a given PO line.
/// </summary>
public sealed class DefaultMatchPolicy : IMatchPolicy
{
    /// <inheritdoc/>
    public MatchResult Evaluate(ThreeWayMatchInput input, MatchPolicyThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(thresholds);

        var lineResults = new List<MatchLineResult>();

        foreach (var poLine in input.PoLines)
        {
            // Find matching invoice line by PO line ID or material code fallback
            var invoiceLine = input.InvoiceLines
                .FirstOrDefault(il => il.PurchaseOrderLineId == poLine.LineId)
                ?? input.InvoiceLines.FirstOrDefault(il =>
                    il.PurchaseOrderLineId == null &&
                    string.Equals(il.MaterialCode, poLine.MaterialCode, StringComparison.OrdinalIgnoreCase));

            var receivedQty = input.ReceivedQuantitiesByPoLineId.TryGetValue(poLine.LineId, out var rq) ? rq : 0;
            var billedQty = invoiceLine?.Quantity ?? 0;
            var billedUnitPrice = invoiceLine?.UnitPrice ?? 0m;

            // DB-P-08: null-price fallback to active PriceList
            var orderedUnitPrice = poLine.UnitPrice;
            if (orderedUnitPrice is null or 0m)
            {
                if (input.FallbackPriceByMaterialCode.TryGetValue(poLine.MaterialCode, out var fallback) && fallback > 0m)
                    orderedUnitPrice = fallback;
            }

            MatchOutcome lineOutcome;
            decimal priceVariancePct;
            int quantityVariance = Math.Abs(billedQty - receivedQty);

            if (orderedUnitPrice is null or 0m)
            {
                // DB-P-08: no price available → Exception (div-zero guard, manual reconciliation required)
                priceVariancePct = 0m;
                lineOutcome = MatchOutcome.Exception;
            }
            else
            {
                // DB-P-08: div-zero guard — if orderedUnitPrice is 0 we already handled above
                priceVariancePct = Math.Abs(billedUnitPrice - orderedUnitPrice.Value) / orderedUnitPrice.Value;

                var qtyInTolerance = quantityVariance <= thresholds.QuantityToleranceAbs;
                var priceInTolerance = priceVariancePct <= thresholds.PriceTolerancePct;

                lineOutcome = (qtyInTolerance && priceInTolerance)
                    ? MatchOutcome.Matched
                    : MatchOutcome.Exception;
            }

            lineResults.Add(new MatchLineResult(
                MaterialCode: poLine.MaterialCode,
                OrderedQuantity: poLine.OrderedQuantity,
                ReceivedQuantity: receivedQty,
                BilledQuantity: billedQty,
                OrderedUnitPrice: orderedUnitPrice ?? 0m,
                BilledUnitPrice: billedUnitPrice,
                QuantityVariance: quantityVariance,
                PriceVariancePct: priceVariancePct,
                LineOutcome: lineOutcome));
        }

        var overallOutcome = lineResults.Any(l => l.LineOutcome == MatchOutcome.Exception)
            ? MatchOutcome.Exception
            : MatchOutcome.Matched;

        var varianceSummary = BuildVarianceSummary(lineResults, overallOutcome);

        return new MatchResult(input.PurchaseOrderId, lineResults, overallOutcome, varianceSummary);
    }

    private static string BuildVarianceSummary(IReadOnlyList<MatchLineResult> lines, MatchOutcome outcome)
    {
        if (outcome == MatchOutcome.Matched)
            return "All lines matched within tolerance.";

        var exceptions = lines.Where(l => l.LineOutcome == MatchOutcome.Exception).ToList();
        var parts = exceptions.Select(l =>
            $"{l.MaterialCode}: qty-var={l.QuantityVariance}, price-var={l.PriceVariancePct:P2}");

        return $"Exception on {exceptions.Count} line(s): {string.Join("; ", parts)}";
    }
}
