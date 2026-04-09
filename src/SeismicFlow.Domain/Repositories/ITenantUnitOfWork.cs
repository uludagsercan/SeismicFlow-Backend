namespace SeismicFlow.Domain.Repositories;

/// <summary>
/// Unit of Work for tenant-scoped operations (Devices, Channels, Readings).
/// Separate from IUnitOfWork which operates on the master database.
/// </summary>
public interface ITenantUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}