using System.Text.Json;
using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Inventory.Contracts.Dtos;
using SpaceOS.Modules.Inventory.Contracts.Providers;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;

public sealed class RecordDeliveryCommandHandler : IRequestHandler<RecordDeliveryCommand, Result>
{
    private readonly IProcurementRepository _repository;
    private readonly IProcurementV2Repository _v2Repository;
    private readonly IInventoryProvider _inventoryProvider;

    public RecordDeliveryCommandHandler(
        IProcurementRepository repository,
        IProcurementV2Repository v2Repository,
        IInventoryProvider inventoryProvider)
    {
        _repository = repository;
        _v2Repository = v2Repository;
        _inventoryProvider = inventoryProvider;
    }

    public async Task<Result> Handle(RecordDeliveryCommand request, CancellationToken ct)
    {
        var order = await _repository.GetPurchaseOrderByIdAsync(request.PurchaseOrderId, ct).ConfigureAwait(false);
        if (order is null)
            return Result.NotFound($"Purchase order {request.PurchaseOrderId} not found.");

        try
        {
            // WORLDS-PROC-PO-FSM: this handler historically combined the Confirmed→Shipped
            // and Shipped→Delivered transitions into one call (no dedicated "mark shipped"
            // endpoint existed). Now that MarkPurchaseOrderShippedCommand can persist the
            // Shipped state on its own, only perform this step if it is still pending —
            // otherwise MarkShipped() would throw on an order that is already Shipped.
            // RecordDelivery() itself remains the sole authority on Shipped→Delivered legality.
            if (order.Status == PurchaseOrderStatus.Confirmed)
            {
                order.MarkShipped();
            }

            order.RecordDelivery(request.ReceivedQuantity);
        }
        catch (InvalidOperationException ex)
        {
            // Same idempotency mechanism as the FSM-transition endpoints: a repeat of the
            // exact same delivery request (or any call against an order that's not Shipped)
            // hits the aggregate's own guard and is rejected here — no duplicate
            // PurchaseOrderDeliveredEvent, no duplicate outbox row, no duplicate inbound booking.
            return Result.Conflict(ex.Message);
        }

        order.PopDomainEvents();

        var delivery = Delivery.Record(
            request.TenantId,
            request.PurchaseOrderId,
            request.ReceivedQuantity,
            DateTime.UtcNow,
            request.Notes,
            request.RecordedBy);

        // Track G + BE-P-01: INSERT outbox in same transaction as delivery
        // DeliveryId is the idempotency key (v1 Delivery has no Lines — Döntés #3c simplified)
        var payload = JsonSerializer.Serialize(new
        {
            TenantId = request.TenantId,
            DeliveryId = delivery.Id,
            PurchaseOrderId = request.PurchaseOrderId,
            MaterialType = order.MaterialType,
            ReceivedQuantity = request.ReceivedQuantity
        });

        var outboxMessage = ProcurementOutboxMessage.Create(
            tenantId: request.TenantId,
            messageType: "InventoryInboundRequested",
            idempotencyKey: delivery.Id,
            payloadJson: payload);

        await _repository.AddDeliveryAsync(delivery, ct).ConfigureAwait(false);
        await _v2Repository.AddOutboxMessageAsync(outboxMessage, ct).ConfigureAwait(false);

        // BE-P-01: ONE SaveChanges — delivery + outbox in same DB transaction
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        // Synchronous path: direct Inventory call (STAYS — Track D worker is the async backup)
        // This call is outside the DB transaction (ADR-039: network I/O after commit)
        try
        {
            var inboundMovement = new StockMovementDto(
                order.MaterialType,
                0m,
                request.ReceivedQuantity,
                (int)Math.Ceiling(request.ReceivedQuantity),
                $"Delivery from PO {request.PurchaseOrderId}",
                DateTime.UtcNow);
            await _inventoryProvider.RecordInboundAsync(inboundMovement, ct).ConfigureAwait(false);
        }
        catch
        {
            // Sync call failure is non-fatal: the outbox worker (Track D, blokkolva)
            // will retry via the outbox row that was committed above.
            // No rollback — the delivery is a physical fact.
        }

        return Result.Success();
    }
}
