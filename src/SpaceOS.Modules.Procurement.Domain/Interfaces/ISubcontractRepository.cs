using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Domain.Interfaces;

/// <summary>
/// Repository interface for subcontract orders.
/// </summary>
public interface ISubcontractRepository
{
    /// <summary>Gets a subcontract order by ID.</summary>
    Task<SubcontractOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gets all subcontract orders for a tenant.</summary>
    Task<IReadOnlyList<SubcontractOrder>> GetByTenantAsync(
        Guid tenantId,
        SubcontractStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Gets subcontract orders for a specific supplier (RLS enforced).</summary>
    Task<IReadOnlyList<SubcontractOrder>> GetBySupplierAsync(
        Guid supplierId,
        SubcontractStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Adds a new subcontract order (generates order number from sequence).</summary>
    Task<Result> AddAsync(SubcontractOrder order, CancellationToken ct = default);

    /// <summary>Updates an existing subcontract order.</summary>
    Task<Result> UpdateAsync(SubcontractOrder order, CancellationToken ct = default);

    /// <summary>Saves changes.</summary>
    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}
