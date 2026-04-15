namespace SeismicFlow.Infrastructure.External.Keycloak
{
    /// <summary>
    /// Configuration for Keycloak Admin REST API.
    /// Bound from appsettings.json section "Keycloak".
    /// </summary>
    public sealed class KeycloakOptions
    {
        public const string Section = "Keycloak";
        public string BaseUrl { get; set; } = default!;
        public string Realm { get; set; } = default!;
        public string AdminClientId { get; set; } = default!;
        public string AdminClientSecret { get; set; } = default!;
        public string AdminUser { get; set; } = "admin";
        public string AdminPassword { get; set; } = "admin";
        public string TenantGroupParentPath { get; set; } = "/tenants";

        public string AdminBaseUrl => $"{BaseUrl}/admin/realms/{Realm}";
        public string TokenUrl => $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token";
    }
}
