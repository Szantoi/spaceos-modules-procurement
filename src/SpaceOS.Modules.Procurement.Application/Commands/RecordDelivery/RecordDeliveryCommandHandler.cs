using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Inventory.Contracts.Dtos;
using SpaceOS.Modules.Inventory.Contracts.Providers;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;

public sealed class RecordDeliveryCommandHandler : IRequestHandler<RecordDeliveryCommand, Result>
{
    private readonly IProcurementRepository _repository;
    private readonly IInventoryProvider _inventoryProvider;

    public RecordDeliveryCommandHandler(IProcurementRepository repository, IInventoryProvider inventoryProvider)
    {
        _repository = repository;
        _inventoryProvider = inventoryProvider;
    }

    public async Task<Result> Handle(RecordDeliveryCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);
        if (order is null)
            return Result.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        order.MarkShipped();
        order.RecordDelivery(request.ReceivedQuantity);
        order.PopDomainEvents();

        var delivery = Delivery.Record(
            request.TenantId,
            request.PurchaseOrderId,
            request.ReceivedQuantity,
            DateTime.UtcNow,
            request.Notes,
            request.RecordedBy);

        await _repository.AddDeliveryAsync(delivery, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        // Integration: record inbound stock in Inventory
        var inboundMovement = new StockMovementDto(
            order.MaterialType,
            0m, // thickness not tracked at order level
            request.ReceivedQuantity,
            (int)Math.Ceiling(request.ReceivedQuantity),
            $"Delivery from PO {request.PurchaseOrderId}",
            DateTime.UtcNow);
        await _inventoryProvider.RecordInboundAsync(inboundMovement, ct).ConfigureAwait(false);

        return Result.Success();
    }
}
