using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetRequisitionById;

public sealed record GetRequisitionByIdQuery(Guid TenantId, Guid RequisitionId) : IRequest<Result<RequisitionDto>>;
