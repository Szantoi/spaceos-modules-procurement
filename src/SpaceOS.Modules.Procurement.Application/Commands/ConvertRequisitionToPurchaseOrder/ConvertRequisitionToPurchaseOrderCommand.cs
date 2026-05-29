using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ConvertRequisitionToPurchaseOrder;

public sealed record ConvertRequisitionToPurchaseOrderCommand(
    Guid TenantId,
    Guid RequisitionId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    string Actor,
    DateTime? ExpectedDeliveryDate = null) : IRequest<Result<Guid>>;
