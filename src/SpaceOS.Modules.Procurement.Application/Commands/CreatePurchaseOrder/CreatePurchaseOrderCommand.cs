using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseOrder;

public sealed record CreatePurchaseOrderCommand(
    Guid TenantId,
    Guid SupplierId,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string Currency,
    DateTime? ExpectedDeliveryDate) : IRequest<Result<Guid>>;
