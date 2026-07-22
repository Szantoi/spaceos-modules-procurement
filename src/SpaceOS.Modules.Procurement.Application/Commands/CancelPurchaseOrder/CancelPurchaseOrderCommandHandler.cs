using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CancelPurchaseOrder;

public sealed class CancelPurchaseOrderCommandHandler
    : IRequestHandler<CancelPurchaseOrderCommand, Result<OrderStatusResponse>>
{
    private readonly IProcurementRepository _repository;

    public CancelPurchaseOrderCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OrderStatusResponse>> Handle(CancelPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);

        if (order is null || order.TenantId != request.TenantId)
            return Result<OrderStatusResponse>.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        try
        {
            order.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrderStatusResponse>.Conflict(ex.Message);
        }

        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<OrderStatusResponse>.Success(OrderStatusResponseFactory.FromOrder(order));
    }
}
