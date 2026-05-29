namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Owned entity: a single material entry in a price list with optional quantity tier.
/// </summary>
public sealed class PriceListEntry
{
    public Guid Id { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Material code — max 20 characters.</summary>
    public string MaterialCode { get; private set; } = string.Empty;

    /// <summary>Unit price — must be positive.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Minimum quantity for this tier (default 1).</summary>
    public int MinQuantity { get; private set; }

    /// <summary>Maximum quantity for this tier (null = open-ended).</summary>
    public int? MaxQuantity { get; private set; }

    private PriceListEntry() { }

    internal static PriceListEntry Create(
        Guid priceListId,
        Guid tenantId,
        string materialCode,
        decimal unitPrice,
        int minQuantity = 1,
        int? maxQuantity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);
        if (materialCode.Length > 20)
            throw new ArgumentException("MaterialCode must be ≤20 characters.", nameof(materialCode));
        if (unitPrice <= 0)
            throw new ArgumentException("UnitPrice must be positive.", nameof(unitPrice));
        if (minQuantity < 1)
            throw new ArgumentException("MinQuantity must be >= 1.", nameof(minQuantity));
        if (maxQuantity.HasValue && maxQuantity.Value < minQuantity)
            throw new ArgumentException("MaxQuantity must be >= MinQuantity.", nameof(maxQuantity));

        return new PriceListEntry
        {
            Id = Guid.NewGuid(),
            PriceListId = priceListId,
            TenantId = tenantId,
            MaterialCode = materialCode,
            UnitPrice = unitPrice,
            MinQuantity = minQuantity,
            MaxQuantity = maxQuantity
        };
    }
}
