using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoiceWithVariance;

public sealed record ApproveInvoiceWithVarianceCommand(
    Guid TenantId,
    Guid InvoiceId,
    string Approver,
    IReadOnlyList<string> UserRoles) : IRequest<Result>;
