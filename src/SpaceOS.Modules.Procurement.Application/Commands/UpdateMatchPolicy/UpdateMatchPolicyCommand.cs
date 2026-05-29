using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.UpdateMatchPolicy;

public sealed record UpdateMatchPolicyCommand(
    Guid TenantId,
    decimal PriceTolerancePct,
    int QuantityToleranceAbs) : IRequest<Result>;
