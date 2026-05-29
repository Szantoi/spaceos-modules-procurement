namespace SpaceOS.Modules.Procurement.Application.Dtos;

public sealed record RequisitionDto(
    Guid Id,
    Guid TenantId,
    string RequisitionNumber,
    string Source,
    string Status,
    string RequestedBy,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectedReason,
    Guid? ConvertedPurchaseOrderId,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<RequisitionLineDto> Lines);

public sealed record RequisitionLineDto(
    Guid Id,
    string MaterialCode,
    int Quantity,
    decimal? EstimatedUnitPrice,
    Guid? PreferredSupplierId,
    string? Notes);
