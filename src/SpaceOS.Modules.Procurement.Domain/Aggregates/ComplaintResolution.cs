using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Final resolution of a complaint by tenant (owned entity).
/// </summary>
public class ComplaintResolution
{
    public ResolutionType Type { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public decimal? FinalAmount { get; private set; }
    public ResolutionAction Action { get; private set; }

    // Audit
    public string ResolvedBy { get; private set; } = string.Empty;
    public DateTime ResolvedAt { get; private set; }

    private ComplaintResolution() { }

    /// <summary>
    /// Creates a complaint resolution.
    /// </summary>
    public static ComplaintResolution Create(
        ResolutionType type,
        string summary,
        decimal? finalAmount,
        ResolutionAction action,
        string resolvedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (summary.Length > 2000)
            throw new ArgumentException("Summary max 2000 characters.", nameof(summary));

        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedBy);

        return new ComplaintResolution
        {
            Type = type,
            Summary = summary,
            FinalAmount = finalAmount,
            Action = action,
            ResolvedBy = resolvedBy,
            ResolvedAt = DateTime.UtcNow
        };
    }
}
