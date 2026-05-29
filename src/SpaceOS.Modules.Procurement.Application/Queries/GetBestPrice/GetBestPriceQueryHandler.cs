using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetBestPrice;

public sealed class GetBestPriceQueryHandler
    : IRequestHandler<GetBestPriceQuery, Result<PriceListEntryDto?>>
{
    private readonly IProcurementV2Repository _repository;

    public GetBestPriceQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PriceListEntryDto?>> Handle(GetBestPriceQuery request, CancellationToken ct)
    {
        // BE-P-06: single Specification query — not in-memory filtering
        var entry = await _repository.GetBestPriceAsync(
            request.TenantId,
            request.MaterialCode,
            request.Quantity,
            request.Currency,
            DateOnly.FromDateTime(DateTime.UtcNow),
            ct).ConfigureAwait(false);

        if (entry is null)
            return Result<PriceListEntryDto?>.Success(null);

        var dto = new PriceListEntryDto(entry.Id, entry.MaterialCode, entry.UnitPrice, entry.MinQuantity, entry.MaxQuantity);
        return Result<PriceListEntryDto?>.Success(dto);
    }
}
