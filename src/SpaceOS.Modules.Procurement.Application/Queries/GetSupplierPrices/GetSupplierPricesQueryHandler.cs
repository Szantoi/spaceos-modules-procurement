using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetSupplierPrices;

public sealed class GetSupplierPricesQueryHandler : IRequestHandler<GetSupplierPricesQuery, Result<IReadOnlyList<SupplierPriceResponse>>>
{
    private readonly IProcurementRepository _repository;

    public GetSupplierPricesQueryHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<SupplierPriceResponse>>> Handle(GetSupplierPricesQuery request, CancellationToken ct)
    {
        var suppliers = await _repository.GetActiveSuppliersByTenantAsync(request.TenantId, ct).ConfigureAwait(false);

        // Simplified: returns active suppliers with a placeholder unit price
        // In Phase 2 this would query a price catalog filtered by materialType
        var prices = suppliers
            .Select(s => new SupplierPriceResponse(s.Id, s.Name, 0m, s.LeadTimeDays))
            .ToList();

        return Result<IReadOnlyList<SupplierPriceResponse>>.Success(prices);
    }
}
