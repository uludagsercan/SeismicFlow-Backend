using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Infrastructure.Persistence.Configurations;

namespace SeismicFlow.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Shared database context — contains only the Tenants table.
    /// One instance, shared across the entire application.
    /// Connection string: "MasterDb" in appsettings.json
    /// </summary>
    public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options)
        : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TenantConfiguration());
        }
    }
}
