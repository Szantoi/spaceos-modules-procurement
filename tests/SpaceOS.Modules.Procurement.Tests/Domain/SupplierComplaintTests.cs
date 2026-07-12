using FluentAssertions;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Domain;

public class SupplierComplaintTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DeliveryId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private const string TestUser = "test@example.com";

    [Fact]
    public void Create_WithValidData_ShouldReturnDraftStatus()
    {
        // Arrange & Act
        var result = SupplierComplaint.Create(
            TenantId,
            SupplierId,
            DeliveryId,
            null,
            ComplaintType.QualityDefect,
            "Quality Issue",
            "Test description",
            10.5m,
            1000m,
            "HUF",
            null,
            null,
            TestUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var complaint = result.Value;
        complaint.Status.Should().Be(ComplaintStatus.Draft);
        complaint.TenantId.Should().Be(TenantId);
        complaint.DeliveryId.Should().Be(DeliveryId);
        complaint.SupplierId.Should().Be(SupplierId);
        complaint.Subject.Should().Be("Quality Issue");
        complaint.Description.Should().Be("Test description");
        complaint.CreatedBy.Should().Be(TestUser);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldFail()
    {
        // Arrange & Act
        var result = SupplierComplaint.Create(
            Guid.Empty,
            SupplierId,
            DeliveryId,
            null,
            ComplaintType.QualityDefect,
            "Subject",
            "Description",
            10m,
            1000m,
            null,
            null,
            null,
            TestUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.ErrorMessage.Contains("TenantId"));
    }

    [Fact]
    public void Submit_FromDraft_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();

        // Act
        var result = complaint.Submit(TestUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.Submitted);
    }

    [Fact]
    public void Submit_FromNonDraft_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);

        // Act - try to submit again
        var result = complaint.Submit(TestUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Withdraw_FromSubmitted_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);

        // Act
        var result = complaint.Withdraw(TestUser, "Customer request");

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.Withdrawn);
    }

    [Fact]
    public void Withdraw_WithoutReason_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();

        // Act
        var result = complaint.Withdraw(TestUser, "");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void MarkAsReviewing_FromSubmitted_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);

        // Act
        var result = complaint.MarkAsReviewing(TestUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.SupplierReviewing);
    }

    [Fact]
    public void MarkAsReviewing_FromDraft_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();

        // Act
        var result = complaint.MarkAsReviewing(TestUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Respond_FromSupplierReviewing_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);
        complaint.MarkAsReviewing(TestUser);

        var response = ComplaintResponse.Create(
            ResponseType.Accept,
            "We will refund",
            1000m,
            null,
            null,
            TestUser);

        // Act
        var result = complaint.Respond(response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.SupplierResponded);
        complaint.SupplierResponse.Should().NotBeNull();
        complaint.SupplierResponse!.Type.Should().Be(ResponseType.Accept);
    }

    [Fact]
    public void Respond_FromDraft_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();
        var response = ComplaintResponse.Create(
            ResponseType.Accept,
            "Response",
            1000m,
            null,
            null,
            TestUser);

        // Act
        var result = complaint.Respond(response);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void AcceptResponse_FromSupplierResponded_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);
        complaint.MarkAsReviewing(TestUser);
        var response = ComplaintResponse.Create(
            ResponseType.Accept,
            "We will refund",
            1000m,
            null,
            null,
            TestUser);
        complaint.Respond(response);

        // Act
        var result = complaint.AcceptResponse(TestUser);

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.UnderReview);
    }

    [Fact]
    public void AcceptResponse_FromDraft_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();

        // Act
        var result = complaint.AcceptResponse(TestUser);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Resolve_FromUnderReview_ShouldSucceed()
    {
        // Arrange
        var complaint = CreateComplaint();
        complaint.Submit(TestUser);
        complaint.MarkAsReviewing(TestUser);
        var response = ComplaintResponse.Create(
            ResponseType.Partial,
            "Partial refund",
            500m,
            null,
            null,
            TestUser);
        complaint.Respond(response);
        complaint.AcceptResponse(TestUser); // Move to UnderReview

        var resolution = ComplaintResolution.Create(
            ResolutionType.Compromised,
            "Final decision",
            2000m,
            ResolutionAction.Replacement,
            TestUser);

        // Act
        var result = complaint.Resolve(resolution);

        // Assert
        result.IsSuccess.Should().BeTrue();
        complaint.Status.Should().Be(ComplaintStatus.Resolved);
    }

    [Fact]
    public void Resolve_FromDraft_ShouldFail()
    {
        // Arrange
        var complaint = CreateComplaint();
        var resolution = ComplaintResolution.Create(
            ResolutionType.Accepted,
            "Rejected by tenant",
            null,
            ResolutionAction.NoAction,
            TestUser);

        // Act
        var result = complaint.Resolve(resolution);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    // Helper method
    private static SupplierComplaint CreateComplaint()
    {
        var result = SupplierComplaint.Create(
            TenantId,
            SupplierId,
            DeliveryId,
            null,
            ComplaintType.QualityDefect,
            "Quality Issue",
            "Test complaint description",
            10.5m,
            1000m,
            "HUF",
            null,
            null,
            TestUser);

        return result.Value;
    }
}
