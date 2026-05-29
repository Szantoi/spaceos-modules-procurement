using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetMatchPolicy;

public sealed class GetMatchPolicyQueryHandler
    : IRequestHandler<GetMatchPolicyQuery, Result<MatchPolicyDto>>
{
    private readonly IProcurementV2Repository _repository;

    public GetMatchPolicyQueryHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<MatchPolicyDto>> Handle(GetMatchPolicyQuery request, CancellationToken ct)
    {
        var policy = await _repository.GetMatchPolicyAsync(request.TenantId, ct).ConfigureAwait(false);

        // Return tenant override or platform defaults
        var dto = policy is not null
            ? new MatchPolicyDto(policy.TenantId, policy.PriceTolerancePct, policy.QuantityToleranceAbs)
            : new MatchPolicyDto(request.TenantId, MatchPolicyThresholds.Default.PriceTolerancePct, MatchPolicyThresholds.Default.QuantityToleranceAbs);

        return Result<MatchPolicyDto>.Success(dto);
    }
}
