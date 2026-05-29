using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Application.Commands.RunMatch;

public sealed record RunMatchCommand(
    Guid TenantId,
    Guid InvoiceId,
    string Actor,
    IReadOnlyList<string> UserRoles) : IRequest<Result<MatchResult>>;
