using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ConvertRequisitionToPurchaseOrder;

public sealed class ConvertRequisitionToPurchaseOrderCommandHandler
    : IRequestHandler<ConvertRequisitionToPurchaseOrderCommand, Result<Guid>>
{
    private readonly IProcurementV2Repository _v2Repository;
    private readonly IProcurementRepository _coreRepository;

    public ConvertRequisitionToPurchaseOrderCommandHandler(
        IProcurementV2Repository v2Repository,
        IProcurementRepository coreRepository)
    {
        _v2Repository = v2Repository;
        _coreRepository = coreRepository;
    }

    public async Task<Result<Guid>> Handle(ConvertRequisitionToPurchaseOrderCommand request, CancellationToken ct)
    {
        var requisition = await _v2Repository.GetRequisitionByIdAsync(request.RequisitionId, ct).ConfigureAwait(false);
        if (requisition is null)
            return Result<Guid>.NotFound($"Requisition {request.RequisitionId} not found.");

        if (requisition.TenantId != request.TenantId)
            return Result<Guid>.Forbidden();

        // Create PO — one-tx: Requisition.ConvertToPurchaseOrder + PurchaseOrder.Create
        var order = PurchaseOrder.Create(
            request.TenantId,
            request.SupplierId,
            request.MaterialType,
            request.Quantity,
            request.UnitPrice,
            request.Currency,
            request.ExpectedDeliveryDate);

        var convertResult = requisition.ConvertToPurchaseOrder(order.Id);
        if (!convertResult.IsSuccess)
            return Result<Guid>.Invalid(convertResult.ValidationErrors.ToArray());

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Actor,
            action: "RequisitionConvertedToPO",
            aggregateType: "PurchaseRequisition",
            aggregateId: requisition.Id,
            afterJson: $"{{\"purchaseOrderId\":\"{order.Id}\"}}");

        // BE-P-01: all in one SaveChanges
        await _coreRepository.AddPurchaseOrderAsync(order, ct).ConfigureAwait(false);
        await _v2Repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _v2Repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(order.Id);
    }
}
