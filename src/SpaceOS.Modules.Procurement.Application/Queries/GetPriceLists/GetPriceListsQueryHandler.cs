using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetPriceLists;

public sealed class GetPriceListsQueryHandler
    : IRequestHandler<GetPriceListsQuery, Result<IReadOnlyList<PriceListDto>>>
{
    private readonly IProcurementV2Repository _repository;

    public GetPriceListsQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PriceListDto>>> Handle(GetPriceListsQuery request, CancellationToken ct)
    {
        var priceLists = await _repository.GetPriceListsByTenantAsync(request.TenantId, ct).ConfigureAwait(false);
        var dtos = priceLists.Select(pl => new PriceListDto(
            pl.Id, pl.TenantId, pl.SupplierId, pl.Currency, pl.ValidFrom, pl.ValidTo,
            pl.Status.ToString(), pl.CreatedAt,
            pl.Entries.Select(e => new PriceListEntryDto(e.Id, e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity)).ToList()
        )).ToList();

        return Result<IReadOnlyList<PriceListDto>>.Success(dtos);
    }
}
