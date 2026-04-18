using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrders;

public sealed record GetOrdersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<PurchaseOrderListResponse>>>;

public sealed record PurchaseOrderListResponse(
    Guid Id,
    string SupplierName,
    decimal TotalAmount,
    DateTime? ExpectedDelivery,
    string Status,
    DateTime CreatedAt);
