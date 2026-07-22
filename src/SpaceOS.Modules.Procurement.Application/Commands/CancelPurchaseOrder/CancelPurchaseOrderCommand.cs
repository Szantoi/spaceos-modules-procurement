using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

namespace SpaceOS.Modules.Procurement.Application.Commands.CancelPurchaseOrder;

/// <summary>
/// WORLDS-PROC-PO-FSM: the cancel branch the domain already allows —
/// Draft/Submitted/Confirmed/Shipped → Cancelled. See
/// <see cref="SpaceOS.Modules.Procurement.Domain.Aggregates.PurchaseOrder.Cancel"/>,
/// which itself refuses a Delivered or already-Cancelled order.
/// </summary>
public sealed record CancelPurchaseOrderCommand(Guid TenantId, Guid PurchaseOrderId)
    : IRequest<Result<OrderStatusResponse>>;
