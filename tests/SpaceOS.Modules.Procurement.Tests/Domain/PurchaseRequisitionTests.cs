using Ardalis.Result;
using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class PurchaseRequisitionTests
{
    private static readonly Guid TenantId = new("10000000-0000-0000-0000-000000000001");

    private static IReadOnlyList<(string, int, decimal?, Guid?, string?)> OneValidLine()
        => new[] { ("WD-001", 10, (decimal?)null, (Guid?)null, (string?)null) };

    private static Result<PurchaseRequisition> CreateDraftManual(
        string requestedBy = "user-a",
        RequisitionSource source = RequisitionSource.Manual)
    {
        return source == RequisitionSource.ReorderAlert
            ? PurchaseRequisition.Create(TenantId, "PR-2026-001", source, null, requestedBy, OneValidLine())
            : PurchaseRequisition.Create(TenantId, "PR-2026-001", source, null, requestedBy, OneValidLine());
    }

    [Fact]
    public void Create_WithValidData_ShouldReturnDraftStatus()
    {
        var result = CreateDraftManual();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RequisitionStatus.Draft);
        result.Value.Id.Should().NotBeEmpty();
        result.Value.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldReturnInvalid()
    {
        var result = PurchaseRequisition.Create(
            Guid.Empty, "PR-001", RequisitionSource.Manual, null, "user-a", OneValidLine());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Approve_FromDraft_ShouldTransitionToApproved()
    {
        var req = CreateDraftManual("user-a").Value;

        var result = req.Approve("user-b");

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(RequisitionStatus.Approved);
        req.ApprovedBy.Should().Be("user-b");
    }

    [Fact]
    public void Approve_WhenApproverEqualsRequester_ShouldReturnForbidden()
    {
        var req = CreateDraftManual("user-a").Value;

        var result = req.Approve("user-a");

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public void Approve_WhenSourceIsReorderAlert_WorkerRequestedBy_AnyApproverAllowed()
    {
        // Source = ReorderAlert — same user as requestedBy is allowed (worker bypass)
        var req = PurchaseRequisition.Create(
            TenantId, "PR-2026-RA1", RequisitionSource.ReorderAlert,
            null, "worker:reorder-alert", OneValidLine()).Value;

        var result = req.Approve("worker:reorder-alert");

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(RequisitionStatus.Approved);
    }

    [Fact]
    public void Approve_FromNonDraft_ShouldReturnError()
    {
        var req = CreateDraftManual("user-a").Value;
        req.Approve("user-b");
        req.Reject("change of mind");

        var result = req.Approve("user-b");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Reject_FromDraft_ShouldTransitionToRejected()
    {
        var req = CreateDraftManual().Value;

        var result = req.Reject("Not needed");

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(RequisitionStatus.Rejected);
        req.RejectedReason.Should().Be("Not needed");
    }

    [Fact]
    public void Reject_FromApproved_ShouldTransitionToRejected()
    {
        var req = CreateDraftManual("user-a").Value;
        req.Approve("user-b");

        var result = req.Reject("Cancellation approved");

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(RequisitionStatus.Rejected);
    }

    [Fact]
    public void ConvertToPurchaseOrder_FromApproved_ShouldTransitionToConvertedToPO()
    {
        var req = CreateDraftManual("user-a").Value;
        req.Approve("user-b");

        var result = req.ConvertToPurchaseOrder(Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        req.Status.Should().Be(RequisitionStatus.ConvertedToPO);
        req.ConvertedPurchaseOrderId.Should().NotBeNull();
    }

    [Fact]
    public void ConvertToPurchaseOrder_FromDraft_ShouldReturnError()
    {
        var req = CreateDraftManual().Value;

        var result = req.ConvertToPurchaseOrder(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        req.Status.Should().Be(RequisitionStatus.Draft);
    }

    [Fact]
    public void Create_ShouldRaisePurchaseRequisitionCreatedEvent()
    {
        var result = CreateDraftManual();

        result.Value.DomainEvents.Should().ContainSingle(e => e is PurchaseRequisitionCreatedEvent);
    }

    [Fact]
    public void Approve_ShouldRaisePurchaseRequisitionApprovedEvent()
    {
        var req = CreateDraftManual("user-a").Value;
        req.PopDomainEvents();

        req.Approve("user-b");

        req.DomainEvents.Should().ContainSingle(e => e is PurchaseRequisitionApprovedEvent);
    }

    [Fact]
    public void Reject_WithEmptyReason_ShouldReturnInvalid()
    {
        var req = CreateDraftManual().Value;

        var result = req.Reject(string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
    }

    [Fact]
    public void Reject_FromConvertedToPO_ShouldReturnError()
    {
        var req = CreateDraftManual("user-a").Value;
        req.Approve("user-b");
        req.ConvertToPurchaseOrder(Guid.NewGuid());

        var result = req.Reject("Too late");

        result.IsSuccess.Should().BeFalse();
        req.Status.Should().Be(RequisitionStatus.ConvertedToPO);
    }

    [Fact]
    public void Create_WithNoLines_ShouldReturnInvalid()
    {
        var result = PurchaseRequisition.Create(
            TenantId, "PR-001", RequisitionSource.Manual, null, "user-a",
            Array.Empty<(string, int, decimal?, Guid?, string?)>());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ConvertToPurchaseOrder_WithEmptyPurchaseOrderId_ShouldReturnInvalid()
    {
        var req = CreateDraftManual("user-a").Value;
        req.Approve("user-b");

        var result = req.ConvertToPurchaseOrder(Guid.Empty);

        result.IsSuccess.Should().BeFalse();
    }
}
