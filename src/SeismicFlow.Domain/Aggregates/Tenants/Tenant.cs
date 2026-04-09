using SeismicFlow.Domain.Common;
using SeismicFlow.Domain.Enums;
using SeismicFlow.Domain.Events;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Domain.Aggregates.Tenants
{
    /// <summary>
    /// Represents a company/organization that owns one or more devices.
    /// Business rules:
    ///   - Slug is immutable after creation
    ///   - Only Provisioning -> Active transition is allowed via Activate()
    /// </summary>
    public sealed class Tenant : AggregateRoot
    {
        public TenantSlug Slug { get; private set; } = default!;
        public string DisplayName { get; private set; } = default!;
        public TenantStatus Status { get; private set; }
        public TenantDatabase Database { get; private set; } = default!;
        public string? KeycloakGroupId { get; private set; }
        public string? KeycloakGroupPath { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public DateTimeOffset? ProvisionedAt { get; private set; }

        private Tenant() { }

        public static Tenant Create(
            TenantSlug slug,
            string displayName,
            TenantDatabase database)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                DisplayName = displayName,
                Database = database,
                Status = TenantStatus.Provisioning,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id, slug.Value));

            return tenant;
        }

        public void Activate(string keycloakGroupId, string keycloakGroupPath)
        {
            if (Status != TenantStatus.Provisioning)
                throw new InvalidOperationException(
                    $"Cannot activate tenant '{Slug}'. Current status: {Status}.");

            KeycloakGroupId = keycloakGroupId;
            KeycloakGroupPath = keycloakGroupPath;
            Status = TenantStatus.Active;
            ProvisionedAt = DateTimeOffset.UtcNow;
            UpdatedAt = DateTimeOffset.UtcNow;

            RaiseDomainEvent(new TenantActivatedEvent(Id, Slug.Value));
        }

        public bool IsActive => Status == TenantStatus.Active;

        // Convenience properties for infrastructure layer
        public string DbHost => Database.Host;
        public int DbPort => Database.Port;
        public string DbName => Database.DbName;
        public string DbUser => Database.DbUser;
    }
}
