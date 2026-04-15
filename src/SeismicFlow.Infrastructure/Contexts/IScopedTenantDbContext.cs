namespace SeismicFlow.Infrastructure.Persistence.Contexts;

/// <summary>
/// Provides a single cached TenantDbContext per DI scope.
/// Both DeviceRepository and TenantUnitOfWork resolve the same instance,
/// ensuring changes tracked by the repository are saved by the UoW.
/// </summary>
public interface IScopedTenantDbContext
{
    /// <summary>
    /// Returns the TenantDbContext for the current tenant.
    /// First call creates the context; subsequent calls return the cached instance.
    /// </summary>
    Task<TenantDbContext> GetAsync(CancellationToken ct = default);
}