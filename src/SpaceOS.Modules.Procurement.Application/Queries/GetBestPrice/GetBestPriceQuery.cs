using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetBestPrice;

public sealed record GetBestPriceQuery(
    Guid TenantId,
    string MaterialCode,
    int Quantity,
    string Currency) : IRequest<Result<PriceListEntryDto?>>;
