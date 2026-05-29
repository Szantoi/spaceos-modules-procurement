namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Owned entity representing a single line in a purchase requisition.
/// </summary>
public sealed class PurchaseRequisitionLine
{
    public Guid Id { get; private set; }
    public Guid RequisitionId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Material code — max 20 characters.</summary>
    public string MaterialCode { get; private set; } = string.Empty;

    /// <summary>Requested quantity — must be positive.</summary>
    public int Quantity { get; private set; }

    /// <summary>Estimated unit price — optional.</summary>
    public decimal? EstimatedUnitPrice { get; private set; }

    /// <summary>Preferred supplier — optional.</summary>
    public Guid? PreferredSupplierId { get; private set; }

    /// <summary>Additional notes — max 500 characters.</summary>
    public string? Notes { get; private set; }

    private PurchaseRequisitionLine() { }

    internal static PurchaseRequisitionLine Create(
        Guid requisitionId,
        Guid tenantId,
        string materialCode,
        int quantity,
        decimal? estimatedUnitPrice,
        Guid? preferredSupplierId,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);
        if (materialCode.Length > 20)
            throw new ArgumentException("MaterialCode must be ≤20 characters.", nameof(materialCode));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (notes?.Length > 500)
            throw new ArgumentException("Notes must be ≤500 characters.", nameof(notes));

        return new PurchaseRequisitionLine
        {
            Id = Guid.NewGuid(),
            RequisitionId = requisitionId,
            TenantId = tenantId,
            MaterialCode = materialCode,
            Quantity = quantity,
            EstimatedUnitPrice = estimatedUnitPrice,
            PreferredSupplierId = preferredSupplierId,
            Notes = notes
        };
    }
}
