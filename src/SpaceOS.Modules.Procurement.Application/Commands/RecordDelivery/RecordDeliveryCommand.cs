using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;

public sealed record RecordDeliveryCommand(
    Guid TenantId,
    Guid PurchaseOrderId,
    decimal ReceivedQuantity,
    string? Notes,
    string RecordedBy) : IRequest<Result>;
