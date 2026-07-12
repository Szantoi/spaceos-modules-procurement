using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Supplier's response to a complaint (owned entity).
/// </summary>
public class ComplaintResponse
{
    public ResponseType Type { get; private set; }
    public string ResponseText { get; private set; } = string.Empty;
    public decimal? OfferedAmount { get; private set; }
    public string? CounterProposal { get; private set; }
    public List<string> AttachmentPaths { get; private set; } = new();

    // Audit
    public string RespondedBy { get; private set; } = string.Empty;
    public DateTime RespondedAt { get; private set; }

    private ComplaintResponse() { }

    /// <summary>
    /// Creates a supplier response to a complaint.
    /// </summary>
    public static ComplaintResponse Create(
        ResponseType type,
        string responseText,
        decimal? offeredAmount,
        string? counterProposal,
        List<string>? attachmentPaths,
        string respondedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseText);
        if (responseText.Length > 3000)
            throw new ArgumentException("ResponseText max 3000 characters.", nameof(responseText));

        if (counterProposal?.Length > 2000)
            throw new ArgumentException("CounterProposal max 2000 characters.", nameof(counterProposal));

        ArgumentException.ThrowIfNullOrWhiteSpace(respondedBy);

        return new ComplaintResponse
        {
            Type = type,
            ResponseText = responseText,
            OfferedAmount = offeredAmount,
            CounterProposal = counterProposal,
            AttachmentPaths = attachmentPaths ?? new List<string>(),
            RespondedBy = respondedBy,
            RespondedAt = DateTime.UtcNow
        };
    }
}
