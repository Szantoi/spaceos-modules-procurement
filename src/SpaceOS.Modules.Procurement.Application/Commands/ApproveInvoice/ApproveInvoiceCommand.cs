using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ApproveInvoice;

public sealed record ApproveInvoiceCommand(
    Guid TenantId,
    Guid InvoiceId,
    string Approver,
    IReadOnlyList<string> UserRoles) : IRequest<Result>;
