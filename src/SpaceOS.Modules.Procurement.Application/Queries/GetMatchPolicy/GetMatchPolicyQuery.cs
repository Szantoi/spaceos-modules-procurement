using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetMatchPolicy;

public sealed record GetMatchPolicyQuery(Guid TenantId) : IRequest<Result<MatchPolicyDto>>;
