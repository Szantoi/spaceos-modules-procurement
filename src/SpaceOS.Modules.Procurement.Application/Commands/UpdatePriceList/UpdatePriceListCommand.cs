using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.UpdatePriceList;

public sealed record UpdatePriceListCommand(
    Guid TenantId,
    Guid PriceListId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PriceListEntryRequest> Entries) : IRequest<Result>;

public sealed record PriceListEntryRequest(
    string MaterialCode,
    decimal UnitPrice,
    int MinQuantity = 1,
    int? MaxQuantity = null);
