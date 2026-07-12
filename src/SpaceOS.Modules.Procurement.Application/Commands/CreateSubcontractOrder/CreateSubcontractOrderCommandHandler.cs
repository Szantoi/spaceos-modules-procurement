using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreateSubcontractOrder;

public sealed class CreateSubcontractOrderCommandHandler
    : IRequestHandler<CreateSubcontractOrderCommand, Result<Guid>>
{
    private readonly ISubcontractRepository _repository;

    public CreateSubcontractOrderCommandHandler(ISubcontractRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CreateSubcontractOrderCommand request, CancellationToken ct)
    {
        var result = SubcontractOrder.Create(
            request.TenantId,
            request.SupplierId,
            request.WorkDescription,
            request.EstimatedCost,
            request.Currency,
            request.Deadline,
            request.CreatedBy);

        if (!result.IsSuccess)
            return Result<Guid>.Invalid(result.ValidationErrors.ToArray());

        var order = result.Value;

        var addResult = await _repository.AddAsync(order, ct).ConfigureAwait(false);
        if (!addResult.IsSuccess)
            return Result<Guid>.Error(string.Join(", ", addResult.Errors));

        var saveResult = await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
            return Result<Guid>.Error(string.Join(", ", saveResult.Errors));

        return Result<Guid>.Success(order.Id);
    }
}
