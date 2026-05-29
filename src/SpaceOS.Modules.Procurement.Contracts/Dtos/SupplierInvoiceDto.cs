namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Full DTO for supplier invoice.</summary>
public sealed record SupplierInvoiceDto(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    string Currency,
    string Status,
    decimal TotalNetAmount,
    decimal TotalGrossAmount);
