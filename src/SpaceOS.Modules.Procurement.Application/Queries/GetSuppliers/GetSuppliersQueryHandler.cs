using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetSuppliers;

public sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, Result<IReadOnlyList<SupplierResponse>>>
{
    private readonly IProcurementRepository _repository;

    public GetSuppliersQueryHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<SupplierResponse>>> Handle(GetSuppliersQuery request, CancellationToken ct)
    {
        var suppliers = await _repository.GetActiveSuppliersByTenantAsync(request.TenantId, ct).ConfigureAwait(false);

        var response = suppliers
            .Select(s => new SupplierResponse(s.Id, s.Name, s.Email, s.Phone, s.Address, s.LeadTimeDays, s.Rating, s.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<SupplierResponse>>.Success(response);
    }
}
