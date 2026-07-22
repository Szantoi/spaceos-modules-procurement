using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

/// <summary>
/// Retrieves the current state of a single purchase order for the given tenant.
/// Tenant isolation: a match on <see cref="OrderId"/> belonging to a different
/// tenant is treated identically to "not found" (no cross-tenant existence leak).
/// </summary>
public sealed record GetOrderStatusQuery(Guid TenantId, Guid OrderId) : IRequest<Result<OrderStatusResponse>>;

/// <summary>
/// Order-detail response for <c>GET /api/procurement/orders/{id}</c>.
/// Reflects only the real fields of the current single-line <c>PurchaseOrder</c>
/// aggregate. A <c>lines[]</c> projection is explicitly out of scope for this
/// task (W5 — multi-line PO redesign is a separate future task).
/// </summary>
public sealed record OrderStatusResponse(
    Guid Id,
    Guid TenantId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    string Status,
    DateTime? ExpectedDelivery,
    DateTime CreatedAt);

/// <summary>
/// WORLDS-PROC-PO-FSM: single mapping point from the <c>PurchaseOrder</c> aggregate to the
/// wire DTO, shared by the read query and every FSM-transition command handler so the
/// "fresh order DTO on success" response shape never drifts between endpoints.
/// </summary>
public static class OrderStatusResponseFactory
{
    public static OrderStatusResponse FromOrder(Domain.Aggregates.PurchaseOrder order) => new(
        order.Id,
        order.TenantId,
        order.SupplierId,
        order.MaterialType,
        order.Quantity,
        order.UnitPrice,
        order.Currency,
        order.Status.ToString(),
        order.ExpectedDeliveryDate,
        order.CreatedAt);
}
