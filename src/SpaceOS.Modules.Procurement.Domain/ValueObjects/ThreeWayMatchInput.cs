namespace SpaceOS.Modules.Procurement.Domain.ValueObjects;

/// <summary>Input data for the three-way match evaluation.</summary>
/// <param name="PurchaseOrderId">The PO being matched.</param>
/// <param name="PoLines">Lines from the purchase order.</param>
/// <param name="ReceivedQuantitiesByPoLineId">Cumulative received quantity per PO line ID (GROUP BY result — BE-P-06).</param>
/// <param name="InvoiceLines">Lines from the supplier invoice.</param>
/// <param name="FallbackPriceByMaterialCode">Active price-list prices for null-UnitPrice fallback (DB-P-08).</param>
public sealed record ThreeWayMatchInput(
    Guid PurchaseOrderId,
    IReadOnlyList<PoLineInput> PoLines,
    IReadOnlyDictionary<Guid, int> ReceivedQuantitiesByPoLineId,
    IReadOnlyList<InvoiceLineInput> InvoiceLines,
    IReadOnlyDictionary<string, decimal> FallbackPriceByMaterialCode);

/// <summary>A single purchase order line for matching.</summary>
public sealed record PoLineInput(
    Guid LineId,
    string MaterialCode,
    int OrderedQuantity,
    decimal? UnitPrice);

/// <summary>A single invoice line for matching.</summary>
public sealed record InvoiceLineInput(
    Guid? PurchaseOrderLineId,
    string MaterialCode,
    int Quantity,
    decimal UnitPrice);
