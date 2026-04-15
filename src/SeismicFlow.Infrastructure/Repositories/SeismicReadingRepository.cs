using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Aggregates.Readings;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence.Repositories;

/// <summary>
/// Used both by HTTP request handlers (via TenantId from HttpContext)
/// and by the background MQTT consumer (tenantId passed explicitly).
/// </summary>
public sealed class SeismicReadingRepository(ITenantDbContextFactory factory)
    : ISeismicReadingRepository
{
    // ── Called from background service with explicit tenantId ─────────────────
    public async Task AddForTenantAsync(
        Guid tenantId,
        SeismicReading reading,
        CancellationToken ct = default)
    {
        var db = await factory.CreateAsync(tenantId, ct);
        db.SeismicReadings.Add(reading);
        await db.SaveChangesAsync(ct);
    }

    // ── ISeismicReadingRepository (HTTP path — tenantId injected separately) ──
    public async Task AddAsync(SeismicReading reading, CancellationToken ct = default)
        => throw new NotSupportedException(
            "Use AddForTenantAsync from the background service. " +
            "HTTP handlers should inject ITenantDbContextFactory directly.");

    public async Task<IReadOnlyList<SeismicReading>> GetByDeviceAsync(
        Guid tenantId,
        Guid deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? channel = null,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var db = await factory.CreateAsync(tenantId, ct);

        var q = db.SeismicReadings
            .Where(r => r.DeviceId == deviceId &&
                        r.Timestamp >= from &&
                        r.Timestamp <= to);

        if (!string.IsNullOrEmpty(channel))
            q = q.Where(r => r.Channel == channel);

        return await q
            .OrderBy(r => r.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<SeismicReading?> GetLatestByDeviceAsync(
        Guid tenantId,
        Guid deviceId,
        string? channel = null,
        CancellationToken ct = default)
    {
        var db = await factory.CreateAsync(tenantId, ct);

        var q = db.SeismicReadings.Where(r => r.DeviceId == deviceId);
        if (!string.IsNullOrEmpty(channel))
            q = q.Where(r => r.Channel == channel);

        return await q
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    // ISeismicReadingRepository interface stubs (HTTP path not used)
    Task<IReadOnlyList<SeismicReading>> ISeismicReadingRepository.GetByDeviceAsync(
        Guid deviceId, DateTimeOffset from, DateTimeOffset to,
        string? channel, int limit, CancellationToken ct)
        => throw new NotSupportedException("Use the overload with tenantId.");

    Task<SeismicReading?> ISeismicReadingRepository.GetLatestByDeviceAsync(
        Guid deviceId, string? channel, CancellationToken ct)
        => throw new NotSupportedException("Use the overload with tenantId.");
}