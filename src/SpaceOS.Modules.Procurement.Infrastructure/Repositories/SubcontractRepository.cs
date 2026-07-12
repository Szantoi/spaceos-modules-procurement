using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Repositories;

/// <summary>
/// Repository for subcontract orders.
/// </summary>
public sealed class SubcontractRepository : ISubcontractRepository
{
    private readonly ProcurementDbContext _db;

    public SubcontractRepository(ProcurementDbContext db)
    {
        _db = db;
    }

    public async Task<SubcontractOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SubcontractOrders
            .FirstOrDefaultAsync(o => o.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<SubcontractOrder>> GetByTenantAsync(
        Guid tenantId,
        SubcontractStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _db.SubcontractOrders.AsNoTracking()
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubcontractOrder>> GetBySupplierAsync(
        Guid supplierId,
        SubcontractStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _db.SubcontractOrders.AsNoTracking()
            .Where(o => o.SupplierId == supplierId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Result> AddAsync(SubcontractOrder order, CancellationToken ct = default)
    {
        try
        {
            // Generate order number using PostgreSQL function
            var year = DateTime.UtcNow.Year;
            var orderNumber = await GenerateOrderNumberAsync(order.TenantId, year, ct).ConfigureAwait(false);
            order.SetOrderNumber(orderNumber);

            await _db.SubcontractOrders.AddAsync(order, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error($"Failed to add subcontract order: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(SubcontractOrder order, CancellationToken ct = default)
    {
        try
        {
            _db.SubcontractOrders.Update(order);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error($"Failed to update subcontract order: {ex.Message}");
        }
    }

    public async Task<Result> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            return Result.Error($"Database save failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Error($"Save failed: {ex.Message}");
        }
    }

    private async Task<string> GenerateOrderNumberAsync(Guid tenantId, int year, CancellationToken ct)
    {
        // Call PostgreSQL function fn_next_subcontract_number
        // Format: SO-YYYY-NNNNN
        var sql = "SELECT fn_next_subcontract_number(@p0, @p1)";
        var parameters = new object[] { tenantId, year };

        var result = await _db.Database
            .SqlQueryRaw<string>(sql, parameters)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return result ?? $"SO-{year}-00001"; // Fallback
    }
}
