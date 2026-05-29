using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.DisputeInvoice;

public sealed record DisputeInvoiceCommand(
    Guid TenantId,
    Guid InvoiceId,
    string Actor,
    string Reason,
    IReadOnlyList<string> UserRoles) : IRequest<Result>;
