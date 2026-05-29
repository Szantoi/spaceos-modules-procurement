using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.ValueObjects;

/// <summary>
/// Aggregate-level result of a three-way match evaluation.
/// Outcome is Exception if any line has Exception outcome.
/// </summary>
public sealed record MatchResult(
    Guid PurchaseOrderId,
    IReadOnlyList<MatchLineResult> Lines,
    MatchOutcome Outcome,
    string VarianceSummary);
