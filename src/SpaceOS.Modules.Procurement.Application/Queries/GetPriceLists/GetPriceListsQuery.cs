using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetPriceLists;

public sealed record GetPriceListsQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<PriceListDto>>>;
