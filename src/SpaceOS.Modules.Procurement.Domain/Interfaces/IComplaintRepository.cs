using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Interfaces;

/// <summary>
/// Repository interface for supplier complaints.
/// </summary>
public interface IComplaintRepository
{
    /// <summary>Gets a complaint by ID.</summary>
    Task<SupplierComplaint?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets all complaints for a tenant (optionally filtered by status/supplier).</summary>
    Task<IReadOnlyList<SupplierComplaint>> GetByTenantAsync(
        Guid tenantId,
        ComplaintStatus? status = null,
        Guid? supplierId = null,
        CancellationToken ct = default);

    /// <summary>Gets complaints for a specific supplier (RLS enforced).</summary>
    Task<IReadOnlyList<SupplierComplaint>> GetBySupplierAsync(
        Guid supplierId,
        ComplaintStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Adds a new complaint (generates complaint number from sequence).</summary>
    Task<Result> AddAsync(SupplierComplaint complaint, CancellationToken ct = default);

    /// <summary>Updates an existing complaint.</summary>
    Task<Result> UpdateAsync(SupplierComplaint complaint, CancellationToken ct = default);

    /// <summary>Saves changes.</summary>
    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}
