using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetOrderStatus;

public sealed record GetOrderStatusQuery(Guid OrderId) : IRequest<Result<OrderStatusResponse>>;
