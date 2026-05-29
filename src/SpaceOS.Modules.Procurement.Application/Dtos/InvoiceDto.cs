namespace SpaceOS.Modules.Procurement.Application.Dtos;

public sealed record InvoiceDto(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Currency,
    string Status,
    decimal TotalNetAmount,
    decimal TotalVatAmount,
    decimal TotalGrossAmount,
    Guid? LatestMatchId,
    string RecordedBy,
    string? VarianceApprovedBy,
    string? DisputeReason,
    DateTime CreatedAt,
    IReadOnlyList<InvoiceLineDto> Lines);

public sealed record InvoiceLineDto(
    Guid Id,
    string MaterialCode,
    Guid? PurchaseOrderLineId,
    int Quantity,
    decimal UnitPrice,
    decimal LineNetAmount,
    decimal LineVatAmount,
    decimal LineGrossAmount);
