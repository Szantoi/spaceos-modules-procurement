using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.AcceptSubcontractOrder;

public sealed class AcceptSubcontractOrderCommandHandler : IRequestHandler<AcceptSubcontractOrderCommand, Result>
{
    private readonly ISubcontractRepository _repository;

    public AcceptSubcontractOrderCommandHandler(ISubcontractRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(AcceptSubcontractOrderCommand request, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, ct).ConfigureAwait(false);
        if (order is null)
            return Result.NotFound();

        var acceptResult = order.Accept();
        if (!acceptResult.IsSuccess)
            return acceptResult;

        var updateResult = await _repository.UpdateAsync(order, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
