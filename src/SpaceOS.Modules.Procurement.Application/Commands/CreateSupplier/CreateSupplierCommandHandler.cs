using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreateSupplier;

public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<CreateSupplierResult>>
{
    private readonly IProcurementRepository _repository;

    public CreateSupplierCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<CreateSupplierResult>> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        var supplier = Supplier.Create(request.TenantId, request.Name, request.Email, request.Phone, request.Address);

        await _repository.AddSupplierAsync(supplier, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<CreateSupplierResult>.Success(
            new CreateSupplierResult(supplier.Id, supplier.Name, supplier.TenantId, supplier.Email, supplier.Phone, supplier.Address, supplier.CreatedAt));
    }
}
