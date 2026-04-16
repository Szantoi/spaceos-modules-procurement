using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<Guid>>
{
    private readonly IProcurementRepository _repository;

    public CreatePurchaseOrderCommandHandler(IProcurementRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        var order = PurchaseOrder.Create(
            request.TenantId,
            request.SupplierId,
            request.MaterialType,
            request.Quantity,
            request.UnitPrice,
            request.Currency,
            request.ExpectedDeliveryDate);

        order.Submit();
        order.PopDomainEvents();

        await _repository.AddPurchaseOrderAsync(order, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(order.Id);
    }
}
