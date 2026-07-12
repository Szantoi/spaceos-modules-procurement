namespace SpaceOS.Modules.Procurement.Domain.Enums;

/// <summary>
/// Type of supplier complaint.
/// </summary>
public enum ComplaintType
{
    /// <summary>Quality defect (rejected goods).</summary>
    QualityDefect = 0,

    /// <summary>Quantity shortage in delivery.</summary>
    QuantityShortage = 1,

    /// <summary>Documentation error (invoice, certificates, etc.).</summary>
    Documentation = 2,

    /// <summary>Damage during delivery.</summary>
    DeliveryDamage = 3,

    /// <summary>Other complaint type.</summary>
    Other = 4
}
