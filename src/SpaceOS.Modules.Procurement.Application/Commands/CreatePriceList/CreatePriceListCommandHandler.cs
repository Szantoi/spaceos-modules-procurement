using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePriceList;

public sealed class CreatePriceListCommandHandler
    : IRequestHandler<CreatePriceListCommand, Result<Guid>>
{
    private readonly IProcurementV2Repository _repository;

    public CreatePriceListCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        var entries = request.Entries
            .Select(e => (e.MaterialCode, e.UnitPrice, e.MinQuantity, e.MaxQuantity))
            .ToList();

        var result = PriceList.Create(
            request.TenantId,
            request.SupplierId,
            request.Currency,
            request.ValidFrom,
            request.ValidTo,
            entries);

        if (!result.IsSuccess)
            return Result<Guid>.Invalid(result.ValidationErrors.ToArray());

        await _repository.AddPriceListAsync(result.Value, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(result.Value.Id);
    }
}
