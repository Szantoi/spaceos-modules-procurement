namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Quality inspection status for delivered goods.
/// </summary>
public enum QualityStatus
{
    /// <summary>All items passed inspection.</summary>
    Passed = 0,

    /// <summary>Some items rejected (partial defect).</summary>
    PartialReject = 1,

    /// <summary>All items rejected (full defect).</summary>
    FullReject = 2
}
