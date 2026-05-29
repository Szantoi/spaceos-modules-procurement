using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePriceList;

public sealed record PriceListEntryRequest(
    string MaterialCode,
    decimal UnitPrice,
    int MinQuantity = 1,
    int? MaxQuantity = null);

public sealed record CreatePriceListCommand(
    Guid TenantId,
    Guid SupplierId,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PriceListEntryRequest> Entries) : IRequest<Result<Guid>>;
