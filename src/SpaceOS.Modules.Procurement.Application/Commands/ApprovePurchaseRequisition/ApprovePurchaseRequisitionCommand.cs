using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApprovePurchaseRequisition;

public sealed record ApprovePurchaseRequisitionCommand(
    Guid TenantId,
    Guid RequisitionId,
    string Approver,
    IReadOnlyList<string> UserRoles) : IRequest<Result>;
