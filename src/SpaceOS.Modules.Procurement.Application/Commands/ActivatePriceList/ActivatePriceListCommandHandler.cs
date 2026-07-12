using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;

namespace SpaceOS.Modules.Procurement.Application.Commands.ActivatePriceList;

public sealed class ActivatePriceListCommandHandler
    : IRequestHandler<ActivatePriceListCommand, Result>
{
    private readonly IProcurementV2Repository _repository;

    public ActivatePriceListCommandHandler(IProcurementV2Repository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(ActivatePriceListCommand request, CancellationToken ct)
    {
        // SEC-P-02: RBAC
        if (!request.UserRoles.Contains("procurement.manager", StringComparer.OrdinalIgnoreCase))
        {
            var forbidAudit = ProcurementAuditLog.Create(
                request.TenantId, request.Actor, "ForbiddenAttempt", "PriceList", request.PriceListId);
            await _repository.AddAuditLogAsync(forbidAudit, ct).ConfigureAwait(false);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Forbidden();
        }

        var priceList = await _repository.GetPriceListByIdAsync(request.PriceListId, ct).ConfigureAwait(false);
        if (priceList is null)
            return Result.NotFound($"Price list {request.PriceListId} not found.");
        if (priceList.TenantId != request.TenantId)
            return Result.Forbidden();

        // DB-P-09: domain-level overlap guard
        var hasOverlap = await _repository.HasOverlappingActivePriceListAsync(
            request.TenantId, priceList.SupplierId, priceList.Currency,
            priceList.ValidFrom, priceList.ValidTo, priceList.Id, ct).ConfigureAwait(false);

        if (hasOverlap)
            return Result.Conflict("An overlapping active price list already exists for this supplier/currency/period.");

        // BE-PROC-001: Auto-expire previous active price lists for same supplier/currency
        var activePriceLists = await _repository.GetActivePriceListsBySupplierAsync(
            request.TenantId, priceList.SupplierId, priceList.Currency, ct).ConfigureAwait(false);

        foreach (var oldPriceList in activePriceLists.Where(pl => pl.Id != priceList.Id))
        {
            var expireResult = oldPriceList.Expire();
            if (!expireResult.IsSuccess)
                return expireResult;
        }

        var activateResult = priceList.Activate();
        if (!activateResult.IsSuccess)
            return activateResult;

        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Actor,
            action: "PriceListActivated",
            aggregateType: "PriceList",
            aggregateId: priceList.Id);

        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }
}
