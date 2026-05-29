namespace SpaceOS.Modules.Procurement.Contracts.Dtos;

/// <summary>Best-price result from active price lists.</summary>
public sealed record PriceListEntryDto(
    Guid EntryId,
    string MaterialCode,
    decimal UnitPrice,
    int MinQuantity,
    int? MaxQuantity);
