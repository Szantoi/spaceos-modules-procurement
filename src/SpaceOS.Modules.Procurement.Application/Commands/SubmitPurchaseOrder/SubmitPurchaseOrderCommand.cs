using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

namespace SpaceOS.Modules.Procurement.Application.Commands.SubmitPurchaseOrder;

/// <summary>
/// WORLDS-PROC-PO-FSM: Draft → Submitted transition. The <c>PurchaseOrder</c>
/// aggregate (<see cref="SpaceOS.Modules.Procurement.Domain.Aggregates.PurchaseOrder.Submit"/>)
/// is the only source of truth for whether this transition is legal — this
/// command never re-implements the guard, it only translates the aggregate's
/// own decision into a <see cref="Result"/>.
/// </summary>
public sealed record SubmitPurchaseOrderCommand(Guid TenantId, Guid PurchaseOrderId)
    : IRequest<Result<OrderStatusResponse>>;
