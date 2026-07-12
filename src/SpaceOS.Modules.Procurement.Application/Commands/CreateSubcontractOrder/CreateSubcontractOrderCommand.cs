using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreateSubcontractOrder;

public sealed record CreateSubcontractOrderCommand(
    Guid TenantId,
    Guid SupplierId,
    string WorkDescription,
    decimal EstimatedCost,
    string? Currency,
    DateTime Deadline,
    string CreatedBy) : IRequest<Result<Guid>>;
