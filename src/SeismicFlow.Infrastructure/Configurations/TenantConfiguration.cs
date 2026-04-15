using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Domain.Enums;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Infrastructure.Persistence.Configurations
{
    public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("tenants");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Slug)
                .HasColumnName("slug")
                .HasMaxLength(64)
                .IsRequired()
                .HasConversion(
                    slug => slug.Value,
                    value => TenantSlug.Create(value));

            builder.HasIndex(t => t.Slug).IsUnique();

            builder.Property(t => t.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasColumnName("status")
                .HasConversion(
                    s => s.ToString(),
                    s => Enum.Parse<TenantStatus>(s))
                .IsRequired();

            // TenantDatabase value object → 4 columns in the same table
            builder.OwnsOne(t => t.Database, db =>
            {
                db.Property(d => d.Host)
                    .HasColumnName("db_host")
                    .HasMaxLength(255)
                    .IsRequired();

                db.Property(d => d.Port)
                    .HasColumnName("db_port")
                    .IsRequired();

                db.Property(d => d.DbName)
                    .HasColumnName("db_name")
                    .HasMaxLength(128)
                    .IsRequired();

                db.Property(d => d.DbUser)
                    .HasColumnName("db_user")
                    .HasMaxLength(128)
                    .IsRequired();
            });

            builder.Property(t => t.KeycloakGroupId)
                .HasColumnName("keycloak_group_id")
                .HasMaxLength(128);

            builder.Property(t => t.KeycloakGroupPath)
                .HasColumnName("keycloak_group_path")
                .HasMaxLength(255);

            builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.Property(t => t.ProvisionedAt).HasColumnName("provisioned_at");

            // Ignore convenience properties — they delegate to Database value object
            builder.Ignore(t => t.DbHost);
            builder.Ignore(t => t.DbPort);
            builder.Ignore(t => t.DbName);
            builder.Ignore(t => t.DbUser);
        }
    }
}
