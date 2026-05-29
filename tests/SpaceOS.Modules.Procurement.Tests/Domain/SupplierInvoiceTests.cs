using Ardalis.Result;
using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using SpaceOS.Modules.Procurement.Domain.ValueObjects;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class SupplierInvoiceTests
{
    private static readonly Guid TenantId = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseOrderId = Guid.NewGuid();

    private static IReadOnlyList<(string, Guid?, int, decimal, decimal, decimal)> ValidLines(
        int qty = 10, decimal unitPrice = 100m)
    {
        var lineNet = Math.Round(qty * unitPrice, 4);
        var lineVat = Math.Round(lineNet * 0.27m, 2);
        return new[] { ("WD-001", (Guid?)null, qty, unitPrice, lineNet, lineVat) };
    }

    private static Result<SupplierInvoice> CreateReceived(
        string recordedBy = "user-a",
        IReadOnlyList<(string, Guid?, int, decimal, decimal, decimal)>? lines = null)
    {
        lines ??= ValidLines();
        return SupplierInvoice.Receive(
            TenantId, SupplierId, PurchaseOrderId,
            "INV-001", DateOnly.FromDateTime(DateTime.Today), null,
            "HUF", recordedBy, lines);
    }

    private static MatchResult BuildMatchResult(MatchOutcome outcome)
    {
        var line = new MatchLineResult(
            MaterialCode: "WD-001",
            OrderedQuantity: 10,
            ReceivedQuantity: 10,
            BilledQuantity: 10,
            OrderedUnitPrice: 100m,
            BilledUnitPrice: 100m,
            QuantityVariance: 0,
            PriceVariancePct: 0m,
            LineOutcome: outcome);

        return new MatchResult(PurchaseOrderId, new[] { line }, outcome,
            outcome == MatchOutcome.Matched ? "All lines matched within tolerance." : "Exception on 1 line(s): WD-001: qty-var=0, price-var=0.00%");
    }

    [Fact]
    public void Receive_WithValidData_ShouldReturnReceivedStatus()
    {
        var result = CreateReceived();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(InvoiceStatus.Received);
        result.Value.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Receive_WithInvalidLineNetAmount_ShouldReturnInvalid()
    {
        // SEC-P-07: LineNetAmount != round(Qty x UnitPrice, 4)
        var lines = new[] { ("WD-001", (Guid?)null, 10, 100m, 999m /* wrong */, 0m) };
        var result = SupplierInvoice.Receive(
            TenantId, SupplierId, PurchaseOrderId,
            "INV-BAD", DateOnly.FromDateTime(DateTime.Today), null,
            "HUF", "user-a", lines);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Receive_WithInvalidTotalAmount_ShouldReturnInvalid()
    {
        // SEC-P-07: amount integrity — empty lines fails
        var result = SupplierInvoice.Receive(
            TenantId, SupplierId, PurchaseOrderId,
            "INV-EMPTY", DateOnly.FromDateTime(DateTime.Today), null,
            "HUF", "user-a", Array.Empty<(string, Guid?, int, decimal, decimal, decimal)>());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RunMatch_WhenInTolerance_ShouldTransitionToMatched()
    {
        var invoice = CreateReceived().Value;

        var result = invoice.RunMatch(BuildMatchResult(MatchOutcome.Matched), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Matched);
    }

    [Fact]
    public void RunMatch_WhenOutOfTolerance_ShouldTransitionToException()
    {
        var invoice = CreateReceived().Value;

        var result = invoice.RunMatch(BuildMatchResult(MatchOutcome.Exception), Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Exception);
    }

    [Fact]
    public void Approve_FromMatched_ShouldTransitionToApproved()
    {
        var invoice = CreateReceived().Value;
        invoice.RunMatch(BuildMatchResult(MatchOutcome.Matched), Guid.NewGuid());

        var result = invoice.Approve("approver-b");

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Approved);
    }

    [Fact]
    public void ApproveWithVariance_FromException_ShouldTransitionToApproved()
    {
        var invoice = CreateReceived("recorder-a").Value;
        invoice.RunMatch(BuildMatchResult(MatchOutcome.Exception), Guid.NewGuid());

        var result = invoice.ApproveWithVariance("approver-b");

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Approved);
        invoice.VarianceApprovedBy.Should().Be("approver-b");
    }

    [Fact]
    public void ApproveWithVariance_WhenVarianceApproverEqualsRecordedBy_ShouldReturnForbidden()
    {
        // SEC-P-03: SoD
        var invoice = CreateReceived("user-a").Value;
        invoice.RunMatch(BuildMatchResult(MatchOutcome.Exception), Guid.NewGuid());

        var result = invoice.ApproveWithVariance("user-a");

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public void Dispute_FromException_ShouldTransitionToDisputed()
    {
        var invoice = CreateReceived().Value;
        invoice.RunMatch(BuildMatchResult(MatchOutcome.Exception), Guid.NewGuid());

        var result = invoice.Dispute("Price too high");

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Disputed);
        invoice.DisputeReason.Should().Be("Price too high");
    }

    [Fact]
    public void Receive_ShouldRaiseSupplierInvoiceReceivedEvent()
    {
        var result = CreateReceived();

        result.Value.DomainEvents.Should().ContainSingle(e => e is SupplierInvoiceReceivedEvent);
    }

    [Fact]
    public void Approve_FromReceived_ShouldReturnError()
    {
        // Approve only allowed from Matched status
        var invoice = CreateReceived().Value;

        var result = invoice.Approve("user-b");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Dispute_FromMatched_ShouldReturnError()
    {
        // Dispute only allowed from Exception status
        var invoice = CreateReceived().Value;
        invoice.RunMatch(BuildMatchResult(MatchOutcome.Matched), Guid.NewGuid());

        var result = invoice.Dispute("Trying to dispute matched invoice");

        result.IsSuccess.Should().BeFalse();
    }
}
