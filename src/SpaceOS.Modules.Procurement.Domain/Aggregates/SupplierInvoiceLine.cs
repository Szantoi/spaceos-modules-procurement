namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Owned entity representing a single line on a supplier invoice.
/// SEC-P-07: amount integrity is enforced by the SupplierInvoice.Receive factory.
/// </summary>
public sealed class SupplierInvoiceLine
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Material code — max 20 characters.</summary>
    public string MaterialCode { get; private set; } = string.Empty;

    /// <summary>Match anchor: the PO line this invoice line is paired to.</summary>
    public Guid? PurchaseOrderLineId { get; private set; }

    /// <summary>Invoiced quantity — must be positive.</summary>
    public int Quantity { get; private set; }

    /// <summary>Unit price as stated on the invoice.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Line net amount = Quantity × UnitPrice (domain-verified).</summary>
    public decimal LineNetAmount { get; private set; }

    /// <summary>Line VAT amount.</summary>
    public decimal LineVatAmount { get; private set; }

    /// <summary>Line gross amount = LineNetAmount + LineVatAmount (domain-verified).</summary>
    public decimal LineGrossAmount { get; private set; }

    private SupplierInvoiceLine() { }

    internal static SupplierInvoiceLine Create(
        Guid invoiceId,
        Guid tenantId,
        string materialCode,
        Guid? purchaseOrderLineId,
        int quantity,
        decimal unitPrice,
        decimal lineNetAmount,
        decimal lineVatAmount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialCode);
        if (materialCode.Length > 20)
            throw new ArgumentException("MaterialCode must be ≤20 characters.", nameof(materialCode));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        return new SupplierInvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            TenantId = tenantId,
            MaterialCode = materialCode,
            PurchaseOrderLineId = purchaseOrderLineId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineNetAmount = lineNetAmount,
            LineVatAmount = lineVatAmount,
            LineGrossAmount = lineNetAmount + lineVatAmount
        };
    }
}
