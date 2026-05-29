using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetInvoices;

public sealed record GetInvoicesQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<InvoiceDto>>>;
