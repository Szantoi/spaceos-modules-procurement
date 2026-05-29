namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Summary DTO for purchase requisition list views.</summary>
public sealed record PurchaseRequisitionSummaryDto(
    Guid Id,
    Guid TenantId,
    string RequisitionNumber,
    string Source,
    string Status,
    string RequestedBy,
    DateTime CreatedAt);

/// <summary>Full DTO for purchase requisition detail view.</summary>
public sealed record PurchaseRequisitionDto(
    Guid Id,
    Guid TenantId,
    string RequisitionNumber,
    string Source,
    Guid? SourceReference,
    string Status,
    string RequestedBy,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectedReason,
    Guid? ConvertedPurchaseOrderId,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<PurchaseRequisitionLineDto> Lines);

/// <summary>Requisition line DTO.</summary>
public sealed record PurchaseRequisitionLineDto(
    Guid Id,
    string MaterialCode,
    int Quantity,
    decimal? EstimatedUnitPrice,
    Guid? PreferredSupplierId);
