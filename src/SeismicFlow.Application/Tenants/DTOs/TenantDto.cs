using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Domain.Enums;

namespace SeismicFlow.Application.Tenants.DTOs
{
    public sealed record TenantDto(
        Guid Id,
        string Slug,
        string DisplayName,
        TenantStatus Status,
        string? KeycloakGroupId,
        string? KeycloakGroupPath,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ProvisionedAt)
    {
        public static TenantDto FromDomain(Tenant tenant) => new(
            Id: tenant.Id,
            Slug: tenant.Slug.Value,
            DisplayName: tenant.DisplayName,
            Status: tenant.Status,
            KeycloakGroupId: tenant.KeycloakGroupId,
            KeycloakGroupPath: tenant.KeycloakGroupPath,
            CreatedAt: tenant.CreatedAt,
            ProvisionedAt: tenant.ProvisionedAt);
    }
}
