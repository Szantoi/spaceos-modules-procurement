using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrders;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, Result<IReadOnlyList<PurchaseOrderListResponse>>>
{
    private readonly IProcurementRepository _repository;

    public GetOrdersQueryHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderListResponse>>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var orders = await _repository.GetOrdersByTenantAsync(request.TenantId, ct).ConfigureAwait(false);
        var suppliers = await _repository.GetActiveSuppliersByTenantAsync(request.TenantId, ct).ConfigureAwait(false);

        var supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);

        var response = orders
            .Select(o => new PurchaseOrderListResponse(
                o.Id,
                supplierNames.TryGetValue(o.SupplierId, out var name) ? name : o.SupplierId.ToString(),
                o.Quantity * o.UnitPrice,
                o.ExpectedDeliveryDate,
                o.Status.ToString(),
                o.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<PurchaseOrderListResponse>>.Success(response);
    }
}
