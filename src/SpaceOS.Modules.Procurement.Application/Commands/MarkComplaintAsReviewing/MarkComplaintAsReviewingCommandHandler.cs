using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.MarkComplaintAsReviewing;

public sealed class MarkComplaintAsReviewingCommandHandler : IRequestHandler<MarkComplaintAsReviewingCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public MarkComplaintAsReviewingCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(MarkComplaintAsReviewingCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        var reviewResult = complaint.MarkAsReviewing(request.ReviewedBy);
        if (!reviewResult.IsSuccess)
            return reviewResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
