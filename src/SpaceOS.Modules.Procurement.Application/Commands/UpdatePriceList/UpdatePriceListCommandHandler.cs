using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.UpdatePriceList;

/// <summary>
/// Updates a price list (only Draft status can be edited).
/// BE-PROC-001: Supplier self-service price list management.
/// </summary>
public sealed class UpdatePriceListCommandHandler
    : IRequestHandler<UpdatePriceListCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public UpdatePriceListCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(UpdatePriceListCommand request, CancellationToken ct)
    {
        var priceList = await _repository.GetPriceListByIdAsync(request.PriceListId, ct).ConfigureAwait(false);
        if (priceList is null)
            return Result.NotFound($"Price list {request.PriceListId} not found.");

        // Tenant isolation check
        if (priceList.TenantId != request.TenantId)
            return Result.Forbidden();

        var entries = request.Entries
            .Select(e => (e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity))
            .ToList();

        var updateResult = priceList.Update(request.ValidFrom, request.ValidTo, entries);
        if (!updateResult.IsSuccess)
            return updateResult;

        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }
}
