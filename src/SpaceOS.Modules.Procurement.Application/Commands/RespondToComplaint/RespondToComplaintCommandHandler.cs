using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.RespondToComplaint;

public sealed class RespondToComplaintCommandHandler : IRequestHandler<RespondToComplaintCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public RespondToComplaintCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(RespondToComplaintCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        ComplaintResponse response;
        try
        {
            response = ComplaintResponse.Create(
                request.ResponseType,
                request.ResponseText,
                request.ProposedValue,
                null, // counterProposal
                null, // attachmentPaths
                request.ResponseProvidedBy);
        }
        catch (ArgumentException ex)
        {
            return Result.Invalid(new ValidationError { ErrorMessage = ex.Message });
        }

        var respondResult = complaint.Respond(response);
        if (!respondResult.IsSuccess)
            return respondResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
