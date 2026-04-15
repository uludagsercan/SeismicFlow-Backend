using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Infrastructure.Persistence.Contexts;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Infrastructure.Persistence;

public sealed class TenantDatabaseProvisioner(
    ILogger<TenantDatabaseProvisioner> logger) : ITenantDatabaseProvisioner
{
    public async Task<Result> ProvisionAsync(Tenant tenant, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Provisioning database for tenant '{Slug}': {DbName}",
            tenant.Slug, tenant.Database.DbName);

        try
        {
            var password = Environment.GetEnvironmentVariable(
                $"TENANT_DB_PASSWORD_{tenant.Slug.Value.Replace("-", "_").ToUpperInvariant()}")
                ?? "postgres"; // dev fallback

            // Master connection to create user and database
            var masterConnStr =
                $"Host={tenant.Database.Host};" +
                $"Port={tenant.Database.Port};" +
                $"Database=seismicflow_master;" +
                $"Username=sf_master;" +
                $"Password=postgres;";

            await using var masterConn = new NpgsqlConnection(masterConnStr);
            await masterConn.OpenAsync(ct);

            // Create user if not exists
            var dbUser = tenant.Database.DbUser;
            var dbName = tenant.Database.DbName;

            await using (var cmd = masterConn.CreateCommand())
            {
                cmd.CommandText = $"""
                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{dbUser}') THEN
                            CREATE ROLE "{dbUser}" LOGIN PASSWORD '{password}';
                        END IF;
                    END
                    $$;
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Create database if not exists
            await using (var cmd = masterConn.CreateCommand())
            {
                cmd.CommandText = $"""
                    SELECT 1 FROM pg_database WHERE datname = '{dbName}'
                    """;
                var exists = await cmd.ExecuteScalarAsync(ct);

                if (exists is null)
                {
                    cmd.CommandText = $"""
                        CREATE DATABASE "{dbName}" OWNER "{dbUser}"
                        """;
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            await masterConn.CloseAsync();

            // Now connect to tenant DB and run EF migrations
            var tenantConnStr =
                $"Host={tenant.Database.Host};" +
                $"Port={tenant.Database.Port};" +
                $"Database={dbName};" +
                $"Username=sf_master;" +
                $"Password=postgres;";

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(tenantConnStr)
                .Options;

            await using var context = new TenantDbContext(options);
            await context.Database.EnsureCreatedAsync(ct);

            // Grant tenant user access to all tables created by sf_master
            await using var grantConn = new NpgsqlConnection(tenantConnStr);
            await grantConn.OpenAsync(ct);
            await using (var cmd = grantConn.CreateCommand())
            {
                cmd.CommandText = $"""
                    GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO "{dbUser}";
                    GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO "{dbUser}";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "{dbUser}";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "{dbUser}";
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            logger.LogInformation(
                "Database provisioned for tenant '{Slug}'.", tenant.Slug);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to provision database for tenant '{Slug}'.", tenant.Slug);

            return Result.Failure(
                Error.ExternalService("DatabaseProvisioner", ex.Message));
        }
    }
}