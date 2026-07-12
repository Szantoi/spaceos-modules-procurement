using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.ValueObjects;

/// <summary>
/// Quality inspection result for a delivery (value object).
/// </summary>
public record QualityInspectionResult
{
    /// <summary>Quality status (Passed, PartialReject, FullReject).</summary>
    public QualityStatus Status { get; init; }

    /// <summary>Accepted quantity.</summary>
    public decimal AcceptedQuantity { get; init; }

    /// <summary>Rejected (defective) quantity.</summary>
    public decimal RejectedQuantity { get; init; }

    /// <summary>Defect description (max 2000 characters).</summary>
    public string? DefectDescription { get; init; }

    /// <summary>MinIO paths to defect photos (max 5 photos).</summary>
    public List<string> DefectPhotoPaths { get; init; } = new();

    /// <summary>
    /// Creates a new quality inspection result.
    /// </summary>
    public static QualityInspectionResult Create(
        QualityStatus status,
        decimal acceptedQuantity,
        decimal rejectedQuantity,
        string? defectDescription,
        List<string>? defectPhotoPaths)
    {
        if (acceptedQuantity < 0)
            throw new ArgumentException("AcceptedQuantity cannot be negative.", nameof(acceptedQuantity));
        if (rejectedQuantity < 0)
            throw new ArgumentException("RejectedQuantity cannot be negative.", nameof(rejectedQuantity));
        if (defectDescription?.Length > 2000)
            throw new ArgumentException("DefectDescription max 2000 characters.", nameof(defectDescription));
        if (defectPhotoPaths?.Count > 5)
            throw new ArgumentException("Maximum 5 defect photos allowed.", nameof(defectPhotoPaths));

        return new QualityInspectionResult
        {
            Status = status,
            AcceptedQuantity = acceptedQuantity,
            RejectedQuantity = rejectedQuantity,
            DefectDescription = defectDescription,
            DefectPhotoPaths = defectPhotoPaths ?? new List<string>()
        };
    }
}
