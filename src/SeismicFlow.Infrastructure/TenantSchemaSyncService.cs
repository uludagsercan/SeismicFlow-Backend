using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence;

/// <summary>
/// Runs on application startup for every existing tenant.
/// 1. Ensures all EF-modelled tables exist (EnsureCreated on new DBs,
///    raw CREATE TABLE IF NOT EXISTS for tables added after initial provisioning)
/// 2. Re-grants permissions to the tenant user so new tables are always accessible.
/// </summary>
public sealed class TenantSchemaSyncService(
    MasterDbContext masterDb,
    ITenantDbContextFactory contextFactory,
    ILogger<TenantSchemaSyncService> logger)
{
    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        var tenants = await masterDb.Tenants.ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            try
            {
                await SyncTenantAsync(
                    tenant.Database.Host,
                    tenant.Database.Port,
                    tenant.Database.DbName,
                    tenant.Database.DbUser,
                    ct);

                logger.LogInformation("Schema sync OK for tenant '{Slug}'", tenant.Slug);
            }
            catch (Exception ex)
            {
                // Non-fatal — log and continue with other tenants
                logger.LogError(ex, "Schema sync FAILED for tenant '{Slug}'", tenant.Slug);
            }
        }
    }

    private static async Task SyncTenantAsync(
        string host, int port, string dbName, string dbUser,
        CancellationToken ct)
    {
        var connStr =
            $"Host={host};Port={port};Database={dbName};" +
            $"Username=sf_master;Password=postgres;";

        // ── 1. Create missing tables ──────────────────────────────────────────
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            // seismic_readings — added in v2, may not exist on older tenant DBs
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS seismic_readings (
                    id          UUID             PRIMARY KEY,
                    device_id   UUID             NOT NULL,
                    channel     VARCHAR(8)       NOT NULL,
                    timestamp   TIMESTAMPTZ      NOT NULL,
                    sample_rate DOUBLE PRECISION NOT NULL,
                    samples     JSONB            NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_seismic_readings_device_channel_time
                    ON seismic_readings (device_id, channel, timestamp);
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── 2. Re-grant permissions (idempotent) ──────────────────────────────
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public TO "{dbUser}";
                GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "{dbUser}";
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES    TO "{dbUser}";
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "{dbUser}";
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}