using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Services;

/// <summary>
/// Domain service for three-way match evaluation.
/// Pure computation — no I/O. Implementations must be stateless.
/// </summary>
public interface IMatchPolicy
{
    /// <summary>
    /// Evaluates the three-way match (PO vs Delivery vs Invoice).
    /// Returns Matched if all lines are within tolerance, Exception otherwise.
    /// </summary>
    MatchResult Evaluate(ThreeWayMatchInput input, MatchPolicyThresholds thresholds);
}
