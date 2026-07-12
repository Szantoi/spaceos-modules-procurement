using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.AcceptComplaintResponse;

public sealed class AcceptComplaintResponseCommandHandler : IRequestHandler<AcceptComplaintResponseCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public AcceptComplaintResponseCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(AcceptComplaintResponseCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        var acceptResult = complaint.AcceptResponse(request.AcceptedBy);
        if (!acceptResult.IsSuccess)
            return acceptResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
