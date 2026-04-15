using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeismicFlow.Domain.Entities;

namespace SeismicFlow.Infrastructure.Persistence.Configurations;

public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(c => c.SampleRate)
            .HasColumnName("sample_rate")
            .IsRequired();

        builder.Property(c => c.FirstSeenAt)
            .HasColumnName("first_seen_at")
            .IsRequired();

        builder.Property(c => c.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();

        // Shadow FK to Device
        builder.Property<Guid>("device_id").IsRequired();

        // Unique: one entry per (device, channel_code)
        // "device_id" → shadow FK property; "Code" → CLR property name (case-sensitive!)
        builder.HasIndex("device_id", nameof(Channel.Code))
            .IsUnique()
            .HasDatabaseName("ix_channels_device_code");
    }
}