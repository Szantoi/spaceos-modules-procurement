using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

public class Supplier : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public int LeadTimeDays { get; private set; }
    public decimal Rating { get; private set; }
    public bool IsActive { get; private set; }

    private Supplier() { }

    public static Supplier Create(Guid tenantId, string name, string contactEmail, int leadTimeDays, decimal rating)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        if (leadTimeDays < 0) throw new ArgumentException("LeadTimeDays cannot be negative.", nameof(leadTimeDays));
        if (rating < 0 || rating > 5) throw new ArgumentException("Rating must be between 0 and 5.", nameof(rating));

        return new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ContactEmail = contactEmail,
            LeadTimeDays = leadTimeDays,
            Rating = rating,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
