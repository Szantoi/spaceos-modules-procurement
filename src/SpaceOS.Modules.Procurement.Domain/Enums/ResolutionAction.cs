namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Action taken as part of complaint resolution.
/// </summary>
public enum ResolutionAction
{
    /// <summary>Credit note issued.</summary>
    CreditNote = 0,

    /// <summary>Replacement goods sent.</summary>
    Replacement = 1,

    /// <summary>Money refund.</summary>
    Refund = 2,

    /// <summary>No action taken (e.g., withdrawn complaint).</summary>
    NoAction = 3
}
