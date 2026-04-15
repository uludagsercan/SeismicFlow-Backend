using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.Aggregates.Readings;
using SeismicFlow.Infrastructure.Persistence.Configurations;

namespace SeismicFlow.Infrastructure.Persistence.Contexts;

/// <summary>
/// Per-tenant database context — contains Devices, Channels and SeismicReadings tables.
/// Created via ITenantDbContextFactory for each tenant request.
/// Each tenant has its own isolated PostgreSQL database.
/// </summary>
public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options)
    : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SeismicReading> SeismicReadings => Set<SeismicReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelConfiguration());
        modelBuilder.ApplyConfiguration(new SeismicReadingConfiguration());
    }
}