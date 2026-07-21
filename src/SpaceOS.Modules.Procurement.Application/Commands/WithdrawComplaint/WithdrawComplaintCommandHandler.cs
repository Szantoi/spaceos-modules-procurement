using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.WithdrawComplaint;

public sealed class WithdrawComplaintCommandHandler : IRequestHandler<WithdrawComplaintCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public WithdrawComplaintCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(WithdrawComplaintCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        var withdrawResult = complaint.Withdraw(request.WithdrawnBy, request.Reason);
        if (!withdrawResult.IsSuccess)
            return withdrawResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
