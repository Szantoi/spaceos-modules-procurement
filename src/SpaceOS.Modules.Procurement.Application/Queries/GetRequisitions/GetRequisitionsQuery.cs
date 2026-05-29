using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetRequisitions;

public sealed record GetRequisitionsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<RequisitionDto>>>;
