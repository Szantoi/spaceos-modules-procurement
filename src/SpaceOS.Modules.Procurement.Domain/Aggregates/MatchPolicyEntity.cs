namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Tenant-level match policy configuration (PK = TenantId).
/// OPEN-05: platform default is ±2% price / ±1 unit quantity.
/// </summary>
public sealed class MatchPolicyEntity
{
    public Guid TenantId { get; private set; }
    public decimal PriceTolerancePct { get; private set; }
    public int QuantityToleranceAbs { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MatchPolicyEntity() { }

    /// <summary>Creates or replaces the match policy for a tenant.</summary>
    public static MatchPolicyEntity Create(Guid tenantId, decimal priceTolerancePct, int quantityToleranceAbs)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (priceTolerancePct < 0) throw new ArgumentException("PriceTolerancePct must be >= 0.", nameof(priceTolerancePct));
        if (quantityToleranceAbs < 0) throw new ArgumentException("QuantityToleranceAbs must be >= 0.", nameof(quantityToleranceAbs));

        return new MatchPolicyEntity
        {
            TenantId = tenantId,
            PriceTolerancePct = priceTolerancePct,
            QuantityToleranceAbs = quantityToleranceAbs,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(decimal priceTolerancePct, int quantityToleranceAbs)
    {
        PriceTolerancePct = priceTolerancePct;
        QuantityToleranceAbs = quantityToleranceAbs;
        UpdatedAt = DateTime.UtcNow;
    }
}
