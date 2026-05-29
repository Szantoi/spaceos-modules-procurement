using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.UpdateMatchPolicy;

public sealed class UpdateMatchPolicyCommandHandler
    : IRequestHandler<UpdateMatchPolicyCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public UpdateMatchPolicyCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(UpdateMatchPolicyCommand request, CancellationToken ct)
    {
        if (request.PriceTolerancePct < 0)
            return Result.Invalid(new ValidationError("PriceTolerancePct must be >= 0."));
        if (request.QuantityToleranceAbs < 0)
            return Result.Invalid(new ValidationError("QuantityToleranceAbs must be >= 0."));

        var policy = MatchPolicyEntity.Create(request.TenantId, request.PriceTolerancePct, request.QuantityToleranceAbs);
        await _repository.UpsertMatchPolicyAsync(policy, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }
}
