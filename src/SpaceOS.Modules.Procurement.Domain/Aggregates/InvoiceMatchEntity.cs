using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Append-only audit snapshot of a three-way match evaluation.
/// DB-P-02: guarded by immutability trigger + REVOKE.
/// </summary>
public sealed class InvoiceMatchEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public string Outcome { get; private set; } = default!;
    public string LineDetailJson { get; private set; } = default!;
    public string VarianceSummary { get; private set; } = default!;
    public decimal PriceTolerancePct { get; private set; }
    public int QuantityToleranceAbs { get; private set; }
    public DateTime EvaluatedAt { get; private set; }

    private InvoiceMatchEntity() { }

    /// <summary>Creates a new invoice match snapshot.</summary>
    public static InvoiceMatchEntity Create(
        Guid tenantId,
        Guid invoiceId,
        Guid purchaseOrderId,
        MatchOutcome outcome,
        string lineDetailJson,
        string varianceSummary,
        decimal priceTolerancePct,
        int quantityToleranceAbs) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InvoiceId = invoiceId,
        PurchaseOrderId = purchaseOrderId,
        Outcome = outcome.ToString(),
        LineDetailJson = lineDetailJson,
        VarianceSummary = varianceSummary,
        PriceTolerancePct = priceTolerancePct,
        QuantityToleranceAbs = quantityToleranceAbs,
        EvaluatedAt = DateTime.UtcNow
    };
}
