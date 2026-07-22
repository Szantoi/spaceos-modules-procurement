using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.MarkPurchaseOrderShipped;

public sealed class MarkPurchaseOrderShippedCommandHandler
    : IRequestHandler<MarkPurchaseOrderShippedCommand, Result<OrderStatusResponse>>
{
    private readonly IProcurementRepository _repository;

    public MarkPurchaseOrderShippedCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OrderStatusResponse>> Handle(MarkPurchaseOrderShippedCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);

        if (order is null || order.TenantId != request.TenantId)
            return Result<OrderStatusResponse>.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        try
        {
            order.MarkShipped();
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrderStatusResponse>.Conflict(ex.Message);
        }

        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<OrderStatusResponse>.Success(OrderStatusResponseFactory.FromOrder(order));
    }
}
