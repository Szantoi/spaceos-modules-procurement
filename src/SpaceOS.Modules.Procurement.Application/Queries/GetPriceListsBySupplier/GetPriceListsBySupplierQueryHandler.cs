using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetPriceListsBySupplier;

public sealed class GetPriceListsBySupplierQueryHandler
    : IRequestHandler<GetPriceListsBySupplierQuery, Result<IReadOnlyList<PriceListDto>>>
{
    private readonly IProcurementV2Repository _repository;

    public GetPriceListsBySupplierQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PriceListDto>>> Handle(
        GetPriceListsBySupplierQuery request, CancellationToken ct)
    {
        var priceLists = await _repository.GetPriceListsBySupplierAsync(
            request.TenantId, request.SupplierId, ct).ConfigureAwait(false);

        var dtos = priceLists.Select(pl => new PriceListDto(
            pl.Id, pl.TenantId, pl.SupplierId, pl.Currency, pl.ValidFrom, pl.ValidTo,
            pl.Status.ToString(), pl.CreatedAt,
            pl.Entries.Select(e => new PriceListEntryDto(
                e.Id, e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity)).ToList()
        )).ToList();

        return Result<IReadOnlyList<PriceListDto>>.Success(dtos);
    }
}
