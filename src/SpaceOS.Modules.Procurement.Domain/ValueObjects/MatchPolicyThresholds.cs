namespace SpaceOS.Modules.Procurement.Domain.ValueObjects;

/// <summary>
/// Platform-default thresholds: ±2% price tolerance, ±1 unit quantity tolerance.
/// </summary>
public sealed record MatchPolicyThresholds(
    decimal PriceTolerancePct,
    int QuantityToleranceAbs)
{
    /// <summary>Platform defaults: ±2% / ±1 unit.</summary>
    public static readonly MatchPolicyThresholds Default = new(0.02m, 1);
}
