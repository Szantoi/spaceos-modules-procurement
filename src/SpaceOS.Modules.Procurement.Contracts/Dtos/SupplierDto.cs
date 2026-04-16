namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Basic supplier profile including supported material types.</summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string ContactEmail,
    string? PhoneNumber,
    IReadOnlyList<string> SupportedMaterials);
