using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.RejectSubcontractOrder;

public sealed record RejectSubcontractOrderCommand(
    Guid OrderId,
    string Reason) : IRequest<Result>;
