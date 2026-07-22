using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

namespace SpaceOS.Modules.Procurement.Application.Commands.ConfirmPurchaseOrder;

/// <summary>
/// WORLDS-PROC-PO-FSM: Submitted → Confirmed transition. See
/// <see cref="SpaceOS.Modules.Procurement.Domain.Aggregates.PurchaseOrder.Confirm"/> —
/// the aggregate is the only source of truth for transition legality.
/// </summary>
public sealed record ConfirmPurchaseOrderCommand(Guid TenantId, Guid PurchaseOrderId)
    : IRequest<Result<OrderStatusResponse>>;
