using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ConfirmPurchaseOrder;

public sealed class ConfirmPurchaseOrderCommandHandler
    : IRequestHandler<ConfirmPurchaseOrderCommand, Result<OrderStatusResponse>>
{
    private readonly IProcurementRepository _repository;

    public ConfirmPurchaseOrderCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OrderStatusResponse>> Handle(ConfirmPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);

        if (order is null || order.TenantId != request.TenantId)
            return Result<OrderStatusResponse>.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        try
        {
            order.Confirm();
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrderStatusResponse>.Conflict(ex.Message);
        }

        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<OrderStatusResponse>.Success(OrderStatusResponseFactory.FromOrder(order));
    }
}
