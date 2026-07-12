using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.SubmitComplaint;

public sealed class SubmitComplaintCommandHandler : IRequestHandler<SubmitComplaintCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public SubmitComplaintCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(SubmitComplaintCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        var submitResult = complaint.Submit(request.SubmittedBy);
        if (!submitResult.IsSuccess)
            return submitResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
