using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ResolveComplaint;

public sealed class ResolveComplaintCommandHandler : IRequestHandler<ResolveComplaintCommand, Result>
{
    private readonly IComplaintRepository _repository;

    public ResolveComplaintCommandHandler(IComplaintRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(ResolveComplaintCommand request, CancellationToken ct)
    {
        var complaint = await _repository.GetByIdAsync(request.ComplaintId, ct).ConfigureAwait(false);
        if (complaint is null)
            return Result.NotFound();

        ComplaintResolution resolution;
        try
        {
            resolution = ComplaintResolution.Create(
                request.ResolutionType,
                request.ResolutionNotes ?? "Resolution applied",
                request.ResolutionValue,
                request.ResolutionAction,
                request.ResolvedBy);
        }
        catch (ArgumentException ex)
        {
            return Result.Invalid(new ValidationError { ErrorMessage = ex.Message });
        }

        var resolveResult = complaint.Resolve(resolution);
        if (!resolveResult.IsSuccess)
            return resolveResult;

        var updateResult = await _repository.UpdateAsync(complaint, ct).ConfigureAwait(false);
        if (!updateResult.IsSuccess)
            return updateResult;

        return await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
