using Microsoft.EntityFrameworkCore;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence.Contexts;

/// <summary>
/// Resolves the tenant's database connection string from the master DB,
/// then creates a TenantDbContext pointing to that database.
/// </summary>
public sealed class TenantDbContextFactory(MasterDbContext masterDb) : ITenantDbContextFactory
{
    public async Task<TenantDbContext> CreateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await masterDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException(
                $"Tenant '{tenantId}' not found in master database.");

        if (!tenant.IsActive)
            throw new InvalidOperationException(
                $"Tenant '{tenant.Slug}' is not active.");

        // Build connection string from tenant metadata
        // Password is retrieved from environment variable or secrets manager
        var password = Environment.GetEnvironmentVariable(
            $"TENANT_DB_PASSWORD_{tenant.Slug.Value.Replace("-", "_").ToUpperInvariant()}")
            ?? "postgres"; // dev fallback — same as provisioner

        var connectionString =
            $"Host={tenant.DbHost};" +
            $"Port={tenant.DbPort};" +
            $"Database={tenant.DbName};" +
            $"Username={tenant.DbUser};" +
            $"Password={password};" +
            $"Application Name=SeismicFlow;";

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TenantDbContext(options);
    }
}