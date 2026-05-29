namespace SpaceOS.Modules.Procurement.Application.Dtos;

public sealed record MatchPolicyDto(
    Guid TenantId,
    decimal PriceTolerancePct,
    int QuantityToleranceAbs);
