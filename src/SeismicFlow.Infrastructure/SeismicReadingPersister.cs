using Microsoft.EntityFrameworkCore;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Domain.Aggregates.Readings;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence;

/// <summary>
/// Persists a SeismicReading AND upserts the channel discovery record.
/// 
/// Channel discovery uses a raw PostgreSQL UPSERT (INSERT ... ON CONFLICT)
/// to avoid loading the Device entity entirely. This eliminates concurrency
/// token issues (xmin) and is more performant: one atomic SQL statement
/// instead of SELECT + UPDATE.
/// </summary>
public sealed class SeismicReadingPersister(ITenantDbContextFactory factory)
    : ISeismicReadingPersister
{
    public async Task PersistAsync(
        Guid tenantId,
        SeismicReading reading,
        CancellationToken ct = default)
    {
        var db = await factory.CreateAsync(tenantId, ct);

        // ── 1. Channel discovery (atomic UPSERT — no concurrency issues) ───
        try
        {
            var now = DateTimeOffset.UtcNow;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO channels (id, device_id, code, sample_rate, first_seen_at, last_seen_at)
                VALUES ({Guid.NewGuid()}, {reading.DeviceId}, {reading.Channel}, {reading.SampleRate}, {now}, {now})
                ON CONFLICT (device_id, code)
                DO UPDATE SET sample_rate  = EXCLUDED.sample_rate,
                              last_seen_at = EXCLUDED.last_seen_at
                """, ct);
        }
        catch (Exception)
        {
            // Channel discovery is best-effort — never lose a reading over it.
        }

        // ── 2. Reading persist (critical — must not be lost) ───────────────
        db.SeismicReadings.Add(reading);
        await db.SaveChangesAsync(ct);
    }
}