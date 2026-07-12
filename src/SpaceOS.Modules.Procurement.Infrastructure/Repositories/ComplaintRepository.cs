using Ardalis.Result;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Interfaces;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Repositories;

/// <summary>
/// Repository for supplier complaints.
/// </summary>
public sealed class ComplaintRepository : IComplaintRepository
{
    private readonly ProcurementDbContext _db;

    public ComplaintRepository(ProcurementDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierComplaint?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SupplierComplaints
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<SupplierComplaint>> GetByTenantAsync(
        Guid tenantId,
        ComplaintStatus? status = null,
        Guid? supplierId = null,
        CancellationToken ct = default)
    {
        var query = _db.SupplierComplaints.AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (supplierId.HasValue)
            query = query.Where(c => c.SupplierId == supplierId.Value);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SupplierComplaint>> GetBySupplierAsync(
        Guid supplierId,
        ComplaintStatus? status = null,
        CancellationToken ct = default)
    {
        var query = _db.SupplierComplaints.AsNoTracking()
            .Where(c => c.SupplierId == supplierId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Result> AddAsync(SupplierComplaint complaint, CancellationToken ct = default)
    {
        try
        {
            // Generate complaint number using PostgreSQL function
            var year = DateTime.UtcNow.Year;
            var complaintNumber = await GenerateComplaintNumberAsync(complaint.TenantId, year, ct).ConfigureAwait(false);
            complaint.SetComplaintNumber(complaintNumber);

            await _db.SupplierComplaints.AddAsync(complaint, ct).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error($"Failed to add complaint: {ex.Message}");
        }
    }

    public async Task<Result> UpdateAsync(SupplierComplaint complaint, CancellationToken ct = default)
    {
        try
        {
            _db.SupplierComplaints.Update(complaint);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error($"Failed to update complaint: {ex.Message}");
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

    private async Task<string> GenerateComplaintNumberAsync(Guid tenantId, int year, CancellationToken ct)
    {
        // Call PostgreSQL function fn_next_complaint_number
        // Format: SC-YYYY-NNNNN
        var sql = "SELECT fn_next_complaint_number(@p0, @p1)";
        var parameters = new object[] { tenantId, year };

        var result = await _db.Database
            .SqlQueryRaw<string>(sql, parameters)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return result ?? $"SC-{year}-00001"; // Fallback (should not happen)
    }
}
