using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

/// <summary>
/// Read-only order-detail query handler. Goes through <see cref="IProcurementRepository"/>
/// (repository port) — no direct DbContext access from the API layer.
/// </summary>
public sealed class GetOrderStatusQueryHandler : IRequestHandler<GetOrderStatusQuery, Result<OrderStatusResponse>>
{
    private readonly IProcurementRepository _repository;

    public GetOrderStatusQueryHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OrderStatusResponse>> Handle(GetOrderStatusQuery request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.OrderId, ct).ConfigureAwait(false);

        // Unknown ID and cross-tenant ID are both reported as NotFound (no existence leak).
        if (order is null || order.TenantId != request.TenantId)
            return Result<OrderStatusResponse>.NotFound($"Purchase order {request.OrderId} not found.");

        var response = new OrderStatusResponse(
            order.Id,
            order.TenantId,
            order.SupplierId,
            order.MaterialType,
            order.Quantity,
            order.UnitPrice,
            order.Currency,
            order.Status.ToString(),
            order.ExpectedDeliveryDate,
            order.CreatedAt);

        return Result<OrderStatusResponse>.Success(response);
    }
}
