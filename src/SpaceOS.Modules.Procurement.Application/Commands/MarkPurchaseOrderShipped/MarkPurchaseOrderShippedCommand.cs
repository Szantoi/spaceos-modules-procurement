using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

namespace SpaceOS.Modules.Procurement.Application.Commands.MarkPurchaseOrderShipped;

/// <summary>
/// WORLDS-PROC-PO-FSM: Confirmed → Shipped transition. See
/// <see cref="SpaceOS.Modules.Procurement.Domain.Aggregates.PurchaseOrder.MarkShipped"/>.
/// Note: <c>RecordDeliveryCommandHandler</c> also performs this same aggregate call
/// internally as a backward-compatible convenience for orders that skip the explicit
/// ship step and go straight from Confirmed to Delivered; that internal call is a
/// no-op once this command has already moved the order to Shipped (see the guard
/// added to <c>RecordDeliveryCommandHandler</c>).
/// </summary>
public sealed record MarkPurchaseOrderShippedCommand(Guid TenantId, Guid PurchaseOrderId)
    : IRequest<Result<OrderStatusResponse>>;
