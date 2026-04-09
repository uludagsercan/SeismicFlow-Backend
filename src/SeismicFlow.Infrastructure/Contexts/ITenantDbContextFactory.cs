namespace SeismicFlow.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Factory that creates a TenantDbContext connected to the correct
    /// tenant database. The connection string is resolved from the master DB
    /// based on the tenant's slug or ID.
    /// </summary>
    public interface ITenantDbContextFactory
    {
        /// <summary>
        /// Creates a TenantDbContext for the given tenant ID.
        /// Looks up the connection string from MasterDbContext.
        /// </summary>
        Task<TenantDbContext> CreateAsync(Guid tenantId, CancellationToken ct = default);
    }
}
