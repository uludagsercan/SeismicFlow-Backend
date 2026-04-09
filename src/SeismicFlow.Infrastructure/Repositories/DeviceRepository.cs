using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence.Repositories;

/// <summary>
/// Device repository operates on the tenant's isolated database.
/// Uses IScopedTenantDbContext to share a single DbContext within the DI scope.
/// 
/// IMPORTANT: Add/Update only track changes — saving is done by ITenantUnitOfWork.
/// </summary>
public sealed class DeviceRepository(IScopedTenantDbContext scopedContext)
    : IDeviceRepository
{
    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var db = await scopedContext.GetAsync(ct);
        return await db.Devices
            .Include(d => d.Channels)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<Device?> GetBySerialNumberAsync(DeviceId serialNumber, CancellationToken ct = default)
    {
        var db = await scopedContext.GetAsync(ct);
        return await db.Devices
            .Include(d => d.Channels)
            .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber, ct);
    }

    public async Task<IReadOnlyList<Device>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var db = await scopedContext.GetAsync(ct);
        return await db.Devices
            .Include(d => d.Channels)
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsBySerialNumberAsync(DeviceId serialNumber, CancellationToken ct = default)
    {
        var db = await scopedContext.GetAsync(ct);
        return await db.Devices.AnyAsync(d => d.SerialNumber == serialNumber, ct);
    }

    public void Add(Device device)
    {
        // Context is already cached — get it synchronously via GetAwaiter().
        // Safe because GetAsync was already called by a query method earlier in the handler.
        var db = scopedContext.GetAsync(default).GetAwaiter().GetResult();
        db.Devices.Add(device);
        // NO SaveChangesAsync here — ITenantUnitOfWork handles persistence.
    }

    public void Update(Device device)
    {
        // Change tracking already knows about this entity (loaded via GetByIdAsync).
        // No explicit Update() call needed — EF Core detects property changes automatically.
        // If the entity was not tracked, the caller should use the repository's query methods first.
    }
}