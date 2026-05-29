using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ActivatePriceList;

public sealed record ActivatePriceListCommand(
    Guid TenantId,
    Guid PriceListId,
    string Actor,
    IReadOnlyList<string> UserRoles) : IRequest<Result>;
