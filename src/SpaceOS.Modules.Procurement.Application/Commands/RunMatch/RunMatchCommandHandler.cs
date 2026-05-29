using System.Text.Json;
using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Domain.Services;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Application.Commands.RunMatch;

public sealed class RunMatchCommandHandler
    : IRequestHandler<RunMatchCommand, Result<MatchResult>>
{
    private readonly IProcurementV2Repository _repository;
    private readonly IMatchPolicy _matchPolicy;

    public RunMatchCommandHandler(IProcurementV2Repository repository, IMatchPolicy matchPolicy)
    {
        _repository = repository;
        _matchPolicy = matchPolicy;
    }

    public async Task<Result<MatchResult>> Handle(RunMatchCommand request, CancellationToken ct)
    {
        var invoice = await _repository.GetInvoiceByIdAsync(request.InvoiceId, ct).ConfigureAwait(false);
        if (invoice is null)
            return Result<MatchResult>.NotFound($"Invoice {request.InvoiceId} not found.");

        if (invoice.TenantId != request.TenantId)
            return Result<MatchResult>.Forbidden();

        // BE-P-06: single GROUP BY query for cumulative received quantities (no N+1)
        var receivedQtys = await _repository.GetReceivedQuantitiesByPoLineAsync(invoice.PurchaseOrderId, ct).ConfigureAwait(false);

        // Load PO lines
        var poLines = await _repository.GetPoLinesAsync(invoice.PurchaseOrderId, ct).ConfigureAwait(false);

        // Load match policy (platform default if no tenant override)
        var policy = await _repository.GetMatchPolicyAsync(request.TenantId, ct).ConfigureAwait(false);
        var thresholds = policy is not null
            ? new MatchPolicyThresholds(policy.PriceTolerancePct, policy.QuantityToleranceAbs)
            : MatchPolicyThresholds.Default;

        // Load fallback prices for lines with null UnitPrice (DB-P-08)
        var fallbackPrices = new Dictionary<string, decimal>();
        foreach (var poLine in poLines.Where(pl => pl.UnitPrice is null or 0m))
        {
            var bestPrice = await _repository.GetBestPriceAsync(
                request.TenantId, poLine.MaterialCode, poLine.OrderedQuantity, invoice.Currency, DateOnly.FromDateTime(DateTime.UtcNow), ct).ConfigureAwait(false);
            if (bestPrice is not null && bestPrice.UnitPrice > 0m)
                fallbackPrices[poLine.MaterialCode] = bestPrice.UnitPrice;
        }

        var invoiceLines = invoice.Lines.Select(il => new InvoiceLineInput(
            il.PurchaseOrderLineId,
            il.MaterialCode,
            il.Quantity,
            il.UnitPrice)).ToList();

        var matchInput = new ThreeWayMatchInput(
            invoice.PurchaseOrderId,
            poLines,
            receivedQtys,
            invoiceLines,
            fallbackPrices);

        // Pure domain computation — no I/O
        var matchResult = _matchPolicy.Evaluate(matchInput, thresholds);

        // Create append-only match snapshot
        var matchEntity = InvoiceMatchEntity.Create(
            request.TenantId,
            invoice.Id,
            invoice.PurchaseOrderId,
            matchResult.Outcome,
            JsonSerializer.Serialize(matchResult.Lines),
            matchResult.VarianceSummary,
            thresholds.PriceTolerancePct,
            thresholds.QuantityToleranceAbs);

        // Update invoice FSM
        var runResult = invoice.RunMatch(matchResult, matchEntity.Id);
        if (!runResult.IsSuccess)
            return Result<MatchResult>.Invalid(runResult.ValidationErrors.ToArray());

        // BE-P-01: match entity + audit + invoice update in one SaveChanges
        var audit = ProcurementAuditLog.Create(
            request.TenantId,
            actor: request.Actor,
            action: matchResult.Outcome == Domain.Enums.MatchOutcome.Matched ? "InvoiceMatched" : "InvoiceMatchException",
            aggregateType: "SupplierInvoice",
            aggregateId: invoice.Id,
            afterJson: $"{{\"outcome\":\"{matchResult.Outcome}\",\"matchId\":\"{matchEntity.Id}\"}}");

        await _repository.AddInvoiceMatchAsync(matchEntity, ct).ConfigureAwait(false);
        await _repository.AddAuditLogAsync(audit, ct).ConfigureAwait(false);
        await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<MatchResult>.Success(matchResult);
    }
}
