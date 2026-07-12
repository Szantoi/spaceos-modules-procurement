using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Application.DTOs;

public record SupplierComplaintDto(
    Guid Id,
    string ComplaintNumber,
    Guid SupplierId,
    Guid DeliveryId,
    ComplaintType Type,
    string Subject,
    string Description,
    ComplaintStatus Status,
    DateTime CreatedAt,
    string CreatedBy);

public record CreateComplaintRequest(
    Guid SupplierId,
    Guid DeliveryId,
    ComplaintType Type,
    string Subject,
    string Description,
    decimal AffectedQuantity,
    decimal? ClaimedAmount,
    string? Currency,
    List<string>? EvidencePaths);
