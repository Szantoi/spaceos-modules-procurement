namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Final resolution type for a complaint.
/// </summary>
public enum ResolutionType
{
    /// <summary>Supplier's response accepted.</summary>
    Accepted = 0,

    /// <summary>Supplier's response rejected (escalation).</summary>
    Rejected = 1,

    /// <summary>Compromised solution.</summary>
    Compromised = 2,

    /// <summary>Complaint withdrawn by tenant.</summary>
    Withdrawn = 3
}
