using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid TenantId, Guid InvoiceId) : IRequest<Result<InvoiceDto>>;
