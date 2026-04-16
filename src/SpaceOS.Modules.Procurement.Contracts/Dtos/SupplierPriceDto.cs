namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>A supplier's current price for a specific material type and thickness.</summary>
public sealed record SupplierPriceDto(
    Guid SupplierId,
    string SupplierName,
    string MaterialType,
    decimal Thickness,
    decimal PricePerPanel,
    string Currency,
    DateTime ValidUntil);
