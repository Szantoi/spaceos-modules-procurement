namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Supplier's response type to a complaint.
/// </summary>
public enum ResponseType
{
    /// <summary>Full acceptance of the complaint.</summary>
    Accept = 0,

    /// <summary>Rejection of the complaint.</summary>
    Reject = 1,

    /// <summary>Partial acceptance.</summary>
    Partial = 2,

    /// <summary>Counter-proposal offered.</summary>
    ProposalCounter = 3
}
