using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.SubmitPurchaseOrder;

public sealed class SubmitPurchaseOrderCommandHandler
    : IRequestHandler<SubmitPurchaseOrderCommand, Result<OrderStatusResponse>>
{
    private readonly IProcurementRepository _repository;

    public SubmitPurchaseOrderCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OrderStatusResponse>> Handle(SubmitPurchaseOrderCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);

        // Unknown ID and cross-tenant ID are both reported as NotFound (no existence leak) —
        // same convention as GetOrderStatusQueryHandler.
        if (order is null || order.TenantId != request.TenantId)
            return Result<OrderStatusResponse>.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        try
        {
            // The aggregate itself decides legality (Draft → Submitted); a repeat of
            // the exact same request on an already-submitted order hits this guard
            // and returns 409 instead of raising a second PurchaseOrderSubmittedEvent —
            // this is the idempotency mechanism (mirrors the Status-guard pattern
            // RecordDelivery already uses).
            order.Submit();
        }
        catch (InvalidOperationException ex)
        {
            return Result<OrderStatusResponse>.Conflict(ex.Message);
        }

        order.PopDomainEvents();
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<OrderStatusResponse>.Success(OrderStatusResponseFactory.FromOrder(order));
    }
}
