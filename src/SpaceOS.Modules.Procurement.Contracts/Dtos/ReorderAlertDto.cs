namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Alert generated when stock drops below reorder threshold, with a suggested order quantity.</summary>
public sealed record ReorderAlertDto(
    Guid TenantId,
    string MaterialType,
    int CurrentStock,
    int ReorderThreshold,
    decimal SuggestedOrderQuantity,
    DateTime GeneratedAt);
