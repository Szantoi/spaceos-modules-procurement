using SpaceOS.Modules.Procurement.Domain.Common;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

public class Supplier : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public int LeadTimeDays { get; private set; }
    public decimal Rating { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Supplier() { }

    public static Supplier Create(Guid tenantId, string name, string email = "", string phone = "", string address = "", int leadTimeDays = 0, decimal rating = 0m)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (leadTimeDays < 0) throw new ArgumentException("LeadTimeDays cannot be negative.", nameof(leadTimeDays));
        if (rating < 0 || rating > 5) throw new ArgumentException("Rating must be between 0 and 5.", nameof(rating));

        return new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Email = email ?? string.Empty,
            Phone = phone ?? string.Empty,
            Address = address ?? string.Empty,
            LeadTimeDays = leadTimeDays,
            Rating = rating,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
