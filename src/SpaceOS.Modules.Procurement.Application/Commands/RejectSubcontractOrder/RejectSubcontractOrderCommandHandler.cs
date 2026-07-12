using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.RejectSubcontractOrder;

public sealed class RejectSubcontractOrderCommandHandler : IRequestHandler<RejectSubcontractOrderCommand, Result>
{
    private readonly ISubcontractRepository _repository;

    public RejectSubcontractOrderCommandHandler(ISubcontractRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(RejectSubcontractOrderCommand request, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, ct).ConfigureAwait(false);
        if (order is null)
            return Result.NotFound();

        var rejectResult = order.Reject(request.Reason);
        if (!rejectResult.IsSuccess)
            return rejectResult;

        var updateResult = await _repository.UpdateAsync(order, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
