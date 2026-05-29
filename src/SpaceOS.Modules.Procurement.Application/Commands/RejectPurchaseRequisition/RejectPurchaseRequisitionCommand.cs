using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.RejectPurchaseRequisition;

public sealed record RejectPurchaseRequisitionCommand(
    Guid TenantId,
    Guid RequisitionId,
    string Actor,
    string Reason) : IRequest<Result>;
