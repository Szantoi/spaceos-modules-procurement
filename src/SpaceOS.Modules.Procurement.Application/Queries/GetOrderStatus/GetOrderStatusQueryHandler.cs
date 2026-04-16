using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

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
        if (order is null)
            return Result<OrderStatusResponse>.NotFound($"Order {request.OrderId} not found.");

        return Result<OrderStatusResponse>.Success(new OrderStatusResponse(
            order.Id,
            order.MaterialType,
            order.Quantity,
            order.Status.ToString(),
            order.ExpectedDeliveryDate));
    }
}
