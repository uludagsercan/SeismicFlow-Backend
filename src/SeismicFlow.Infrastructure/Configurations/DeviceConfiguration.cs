using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeismicFlow.Domain.Aggregates.Devices;
using SeismicFlow.Domain.Enums;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Infrastructure.Persistence.Configurations
{
    public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("devices");

            builder.HasKey(d => d.Id);

            // DeviceId value object → stored as a single column
            builder.Property(d => d.SerialNumber)
                .HasColumnName("serial_number")
                .HasMaxLength(128)
                .IsRequired()
                .HasConversion(
                    id => id.Value,
                    value => DeviceId.Create(value));

            builder.HasIndex(d => d.SerialNumber).IsUnique();

            builder.Property(d => d.TenantId)
                .HasColumnName("tenant_id")
                .IsRequired();

            builder.Property(d => d.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            // DeviceLocation value object → stored as owned entity (3 columns)
            builder.OwnsOne(d => d.Location, loc =>
            {
                loc.Property(l => l.Latitude).HasColumnName("latitude").IsRequired();
                loc.Property(l => l.Longitude).HasColumnName("longitude").IsRequired();
                loc.Property(l => l.Altitude).HasColumnName("altitude").IsRequired();
            });

            // IrisMapping value object → stored as owned entity (2 columns)
            builder.OwnsOne(d => d.IrisMapping, iris =>
            {
                iris.Property(i => i.NetworkCode)
                    .HasColumnName("iris_network_code")
                    .HasMaxLength(8)
                    .IsRequired();

                iris.Property(i => i.StationCode)
                    .HasColumnName("iris_station_code")
                    .HasMaxLength(16)
                    .IsRequired();
            });

            builder.Property(d => d.Status)
                .HasColumnName("status")
                .HasConversion(
                    s => s.ToString(),
                    s => Enum.Parse<DeviceStatus>(s))
                .IsRequired();

            builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();

            // Channels — one-to-many, owned by Device
            builder.HasMany(d => d.Channels)
                .WithOne()
                .HasForeignKey("device_id")
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
