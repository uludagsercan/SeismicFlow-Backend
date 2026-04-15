using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SeismicFlow.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Design-time factory for EF Core CLI tools.
    /// Connection string is hardcoded here — only used for migrations, never in production.
    /// </summary>
    public sealed class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
    {
        public MasterDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<MasterDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=seismicflow_master;Username=sf_master;Password=postgres;")
                .Options;

            return new MasterDbContext(options);
        }
    }
}
