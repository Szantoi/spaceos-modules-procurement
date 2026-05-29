using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Supplier invoice aggregate root.
/// FSM: Received → Matched | Exception → Approved | Disputed (all terminal)
/// SEC-P-07: amount integrity invariant enforced in Receive factory.
/// </summary>
public sealed class SupplierInvoice : AggregateRoot
{
    private readonly List<SupplierInvoiceLine> _lines = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>Supplier's own invoice number — stored normalized (UPPER+trim, DB-P-11).</summary>
    public string SupplierInvoiceNumber { get; private set; } = string.Empty;

    public DateOnly InvoiceDate { get; private set; }
    public DateOnly? DueDate { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    public InvoiceStatus Status { get; private set; }
    public decimal TotalNetAmount { get; private set; }
    public decimal TotalVatAmount { get; private set; }
    public decimal TotalGrossAmount { get; private set; }

    /// <summary>Last invoice_match snapshot ID (audit).</summary>
    public Guid? LatestMatchId { get; private set; }

    /// <summary>Actor who recorded this invoice (SoD anchor, SEC-P-03).</summary>
    public string RecordedBy { get; private set; } = string.Empty;

    /// <summary>Actor who approved with variance — must differ from RecordedBy (SEC-P-03).</summary>
    public string? VarianceApprovedBy { get; private set; }

    public string? DisputeReason { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Read-only view of invoice lines.</summary>
    public IReadOnlyList<SupplierInvoiceLine> Lines => _lines.AsReadOnly();

    private SupplierInvoice() { }

    /// <summary>
    /// Records a new supplier invoice.
    /// SEC-P-07: validates LineNetAmount == round(Qty × UnitPrice, 4) and Total* == Σlines.
    /// DB-P-11: SupplierInvoiceNumber is stored UPPER+trimmed.
    /// </summary>
    public static Result<SupplierInvoice> Receive(
        Guid tenantId,
        Guid supplierId,
        Guid purchaseOrderId,
        string supplierInvoiceNumber,
        DateOnly invoiceDate,
        DateOnly? dueDate,
        string currency,
        string recordedBy,
        IReadOnlyList<(string MaterialCode, Guid? PurchaseOrderLineId, int Quantity, decimal UnitPrice, decimal LineNetAmount, decimal LineVatAmount)> lines)
    {
        if (tenantId == Guid.Empty)
            return Result<SupplierInvoice>.Invalid(new ValidationError("TenantId is required."));
        if (supplierId == Guid.Empty)
            return Result<SupplierInvoice>.Invalid(new ValidationError("SupplierId is required."));
        if (purchaseOrderId == Guid.Empty)
            return Result<SupplierInvoice>.Invalid(new ValidationError("PurchaseOrderId is required."));
        if (string.IsNullOrWhiteSpace(supplierInvoiceNumber))
            return Result<SupplierInvoice>.Invalid(new ValidationError("SupplierInvoiceNumber is required."));
        if (!System.Text.RegularExpressions.Regex.IsMatch(currency, @"^[A-Z]{3}$"))
            return Result<SupplierInvoice>.Invalid(new ValidationError("Currency must be a valid ISO 4217 code."));
        if (string.IsNullOrWhiteSpace(recordedBy))
            return Result<SupplierInvoice>.Invalid(new ValidationError("RecordedBy is required."));
        if (lines is null || lines.Count == 0)
            return Result<SupplierInvoice>.Invalid(new ValidationError("At least one line is required."));

        // SEC-P-07: validate amount integrity per line
        foreach (var (mc, _, qty, unitPrice, lineNet, lineVat) in lines)
        {
            var expectedNet = Math.Round(qty * unitPrice, 4);
            if (lineNet != expectedNet)
                return Result<SupplierInvoice>.Invalid(new ValidationError(
                    $"Line {mc}: LineNetAmount ({lineNet}) does not equal round(Qty×UnitPrice, 4) = {expectedNet}."));
        }

        var totalNet = lines.Sum(l => l.LineNetAmount);
        var totalVat = lines.Sum(l => l.LineVatAmount);
        var totalGross = totalNet + totalVat;

        var invoice = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            PurchaseOrderId = purchaseOrderId,
            SupplierInvoiceNumber = supplierInvoiceNumber.Trim().ToUpperInvariant(),
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Currency = currency,
            Status = InvoiceStatus.Received,
            TotalNetAmount = totalNet,
            TotalVatAmount = totalVat,
            TotalGrossAmount = totalGross,
            RecordedBy = recordedBy,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (mc, poLineId, qty, unitPrice, lineNet, lineVat) in lines)
        {
            var line = SupplierInvoiceLine.Create(invoice.Id, tenantId, mc, poLineId, qty, unitPrice, lineNet, lineVat);
            invoice._lines.Add(line);
        }

        invoice.RaiseDomainEvent(new SupplierInvoiceReceivedEvent(invoice.Id, tenantId, supplierId, purchaseOrderId));
        return Result<SupplierInvoice>.Success(invoice);
    }

    /// <summary>
    /// Records the result of a three-way match.
    /// Transitions to Matched or Exception depending on the MatchResult outcome.
    /// </summary>
    public Result RunMatch(MatchResult matchResult, Guid matchId)
    {
        if (matchResult is null) throw new ArgumentNullException(nameof(matchResult));
        if (matchId == Guid.Empty) throw new ArgumentException("MatchId required.", nameof(matchId));

        if (Status != InvoiceStatus.Received)
            return Result.Invalid(new ValidationError($"Cannot run match on invoice in status {Status}."));

        LatestMatchId = matchId;

        if (matchResult.Outcome == Enums.MatchOutcome.Matched)
        {
            Status = InvoiceStatus.Matched;
            RaiseDomainEvent(new SupplierInvoiceMatchedEvent(Id, TenantId, matchId));
        }
        else
        {
            Status = InvoiceStatus.Exception;
            RaiseDomainEvent(new SupplierInvoiceMatchExceptionEvent(Id, TenantId, matchId, matchResult.VarianceSummary));
        }

        return Result.Success();
    }

    /// <summary>
    /// Approves a matched invoice.
    /// Guard: Status must be Matched.
    /// </summary>
    public Result Approve(string approver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approver);

        if (Status != InvoiceStatus.Matched)
            return Result.Invalid(new ValidationError($"Cannot approve invoice in status {Status}. Use ApproveWithVariance for Exception status."));

        Status = InvoiceStatus.Approved;
        RaiseDomainEvent(new SupplierInvoiceApprovedEvent(Id, TenantId, approver, false));
        return Result.Success();
    }

    /// <summary>
    /// Approves an invoice with variance override.
    /// Guard: Status must be Exception.
    /// SoD (SEC-P-03): approver must differ from RecordedBy.
    /// </summary>
    public Result ApproveWithVariance(string approver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approver);

        if (Status != InvoiceStatus.Exception)
            return Result.Invalid(new ValidationError($"Cannot approve with variance: invoice is in status {Status}."));

        // SoD: variance approver must differ from recorder (SEC-P-03)
        if (string.Equals(approver, RecordedBy, StringComparison.OrdinalIgnoreCase))
            return Result.Forbidden();

        Status = InvoiceStatus.Approved;
        VarianceApprovedBy = approver;
        RaiseDomainEvent(new SupplierInvoiceApprovedEvent(Id, TenantId, approver, true));
        return Result.Success();
    }

    /// <summary>
    /// Disputes an invoice in Exception state.
    /// Guard: Status must be Exception.
    /// </summary>
    public Result Dispute(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Invalid(new ValidationError("Dispute reason is required."));
        if (reason.Length > 2000)
            return Result.Invalid(new ValidationError("Dispute reason must be ≤2000 characters."));

        if (Status != InvoiceStatus.Exception)
            return Result.Invalid(new ValidationError($"Cannot dispute invoice in status {Status}."));

        Status = InvoiceStatus.Disputed;
        DisputeReason = reason;
        RaiseDomainEvent(new SupplierInvoiceDisputedEvent(Id, TenantId, reason));
        return Result.Success();
    }
}
