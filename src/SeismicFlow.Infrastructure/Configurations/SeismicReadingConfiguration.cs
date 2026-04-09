using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeismicFlow.Domain.Aggregates.Readings;

namespace SeismicFlow.Infrastructure.Persistence.Configurations;

public sealed class SeismicReadingConfiguration : IEntityTypeConfiguration<SeismicReading>
{
    public void Configure(EntityTypeBuilder<SeismicReading> builder)
    {
        builder.ToTable("seismic_readings");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(r => r.Channel)
            .HasColumnName("channel")
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(r => r.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(r => r.SampleRate)
            .HasColumnName("sample_rate")
            .IsRequired();

        // Store samples as JSONB for compact storage + easy querying
        builder.Property(r => r.Samples)
            .HasColumnName("samples")
            .HasColumnType("jsonb")
            .IsRequired();

        // Optimise for time-series queries: device + channel + time range
        builder.HasIndex(r => new { r.DeviceId, r.Channel, r.Timestamp })
            .HasDatabaseName("ix_seismic_readings_device_channel_time");
    }
}