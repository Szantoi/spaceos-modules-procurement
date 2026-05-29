namespace SpaceOS.Modules.Procurement.Application.Dtos;

public sealed record PriceListDto(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<PriceListEntryDto> Entries);

public sealed record PriceListEntryDto(
    Guid Id,
    string MaterialCode,
    decimal UnitPrice,
    int MinQuantity,
    int? MaxQuantity);
