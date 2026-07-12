namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Subcontract order lifecycle status.
/// </summary>
public enum SubcontractStatus
{
    /// <summary>Pending partner acceptance.</summary>
    Pending = 0,

    /// <summary>Accepted by partner.</summary>
    Accepted = 1,

    /// <summary>Rejected by partner.</summary>
    Rejected = 2,

    /// <summary>Work in progress.</summary>
    InProgress = 3,

    /// <summary>Work completed.</summary>
    Completed = 4,

    /// <summary>Cancelled by tenant.</summary>
    Cancelled = 5
}
