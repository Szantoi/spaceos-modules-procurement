using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.ValueObjects;

/// <summary>
/// Immutable result for a single material line in a three-way match evaluation.
/// ReceivedQuantity is the cumulative total across all Delivery lines for the given PO line (OPEN-07).
/// </summary>
public sealed record MatchLineResult(
    string MaterialCode,
    int OrderedQuantity,
    int ReceivedQuantity,
    int BilledQuantity,
    decimal OrderedUnitPrice,
    decimal BilledUnitPrice,
    int QuantityVariance,
    decimal PriceVariancePct,
    MatchOutcome LineOutcome);
