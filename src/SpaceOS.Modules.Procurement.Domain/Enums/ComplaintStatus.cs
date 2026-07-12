namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Supplier complaint lifecycle status.
/// </summary>
public enum ComplaintStatus
{
    /// <summary>Draft (editable by tenant).</summary>
    Draft = 0,

    /// <summary>Submitted to supplier.</summary>
    Submitted = 1,

    /// <summary>Supplier is reviewing the complaint.</summary>
    SupplierReviewing = 2,

    /// <summary>Supplier has responded.</summary>
    SupplierResponded = 3,

    /// <summary>Tenant is reviewing supplier's response.</summary>
    UnderReview = 4,

    /// <summary>Resolved (terminal state).</summary>
    Resolved = 5,

    /// <summary>Escalated (terminal state).</summary>
    Escalated = 6,

    /// <summary>Withdrawn by tenant (terminal state).</summary>
    Withdrawn = 7
}
