using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.AcceptSubcontractOrder;

public sealed record AcceptSubcontractOrderCommand(Guid OrderId) : IRequest<Result>;
