using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SeismicFlow.Infrastructure.External.Keycloak;

/// <summary>
/// Bootstraps Keycloak realm on startup.
/// Creates realm, clients, roles, mappers, and groups.
/// Idempotent — safe to run multiple times.
/// </summary>
public sealed class KeycloakRealmBootstrapper(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakRealmBootstrapper> logger)
{
    private readonly KeycloakOptions _opts = options.Value;
    private string _adminToken = string.Empty;

    public static readonly string[] Roles =
        ["super-admin", "tenant-admin", "device-manager", "tenant-user"];

    public async Task BootstrapAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting Keycloak realm bootstrap...");
        try
        {
            await GetMasterTokenAsync(ct);
            await EnsureRealmAsync(ct);
            await EnsureApiClientAsync(ct);
            await EnsureAdminClientAsync(ct);
            foreach (var role in Roles)
                await EnsureRoleAsync(role, ct);
            await EnsureTenantIdMapperAsync(ct);
            await EnsureAudienceMapperAsync(ct);
            await EnsureRolesMapperAsync(ct);
            await EnsureTenantsGroupAsync(ct);
            await EnsureTenantIdUserProfileAttributeAsync(ct);
            logger.LogInformation("Keycloak bootstrap completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak bootstrap failed.");
        }
    }

    // ── Master Token ──────────────────────────────────────────────────────────

    private async Task GetMasterTokenAsync(CancellationToken ct)
    {
        var url = $"{_opts.BaseUrl}/realms/master/protocol/openid-connect/token";
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _opts.AdminUser,
            ["password"] = _opts.AdminPassword
        });
        var response = await httpClient.PostAsync(url, body, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        _adminToken = result!.AccessToken;
    }

    // ── Realm ─────────────────────────────────────────────────────────────────

    private async Task EnsureRealmAsync(CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get, $"{_opts.BaseUrl}/admin/realms/{_opts.Realm}");
        var response = await httpClient.SendAsync(req, ct);
        if (response.IsSuccessStatusCode) { logger.LogDebug("Realm exists."); return; }

        logger.LogInformation("Creating realm '{Realm}'...", _opts.Realm);
        using var create = Auth(HttpMethod.Post, $"{_opts.BaseUrl}/admin/realms");
        create.Content = JsonContent.Create(new
        {
            realm = _opts.Realm,
            enabled = true,
            displayName = "SeismicFlow",
            accessTokenLifespan = 3600
        });
        (await httpClient.SendAsync(create, ct)).EnsureSuccessStatusCode();
    }

    // ── seismicflow-api client ────────────────────────────────────────────────

    private async Task EnsureApiClientAsync(CancellationToken ct)
    {
        if (await ClientExistsAsync("seismicflow-api", ct)) return;
        logger.LogInformation("Creating seismicflow-api client...");
        using var req = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/clients");
        req.Content = JsonContent.Create(new
        {
            clientId = "seismicflow-api",
            enabled = true,
            protocol = "openid-connect",
            publicClient = true,
            directAccessGrantsEnabled = true,
            standardFlowEnabled = true
        });
        (await httpClient.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    // ── seismicflow-admin client (service account) ────────────────────────────

    private async Task EnsureAdminClientAsync(CancellationToken ct)
    {
        if (await ClientExistsAsync(_opts.AdminClientId, ct)) return;
        logger.LogInformation("Creating admin client...");
        using var req = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/clients");
        req.Content = JsonContent.Create(new
        {
            clientId = _opts.AdminClientId,
            enabled = true,
            protocol = "openid-connect",
            publicClient = false,
            clientAuthenticatorType = "client-secret",
            secret = _opts.AdminClientSecret,
            serviceAccountsEnabled = true,
            directAccessGrantsEnabled = false,
            standardFlowEnabled = false
        });
        (await httpClient.SendAsync(req, ct)).EnsureSuccessStatusCode();
        await AssignRealmManagementRolesAsync(ct);
    }

    private async Task AssignRealmManagementRolesAsync(CancellationToken ct)
    {
        var saUser = await GetServiceAccountUserAsync(ct);
        if (saUser is null) return;

        var rmClientId = await GetClientInternalIdAsync("realm-management", ct);
        if (rmClientId is null) return;

        var roleNames = new[]
        {
            "manage-users", "manage-groups", "view-groups",
            "query-groups", "query-users", "view-users", "manage-realm"
        };
        var roles = await GetClientRolesAsync(rmClientId, roleNames, ct);
        if (roles.Count == 0) return;

        using var req = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/users/{saUser.Id}/role-mappings/clients/{rmClientId}");
        req.Content = JsonContent.Create(roles);
        await httpClient.SendAsync(req, ct);
        logger.LogInformation("realm-management roles assigned to service account.");
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    private async Task EnsureRoleAsync(string name, CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get, $"{_opts.AdminBaseUrl}/roles/{name}");
        var response = await httpClient.SendAsync(req, ct);
        if (response.IsSuccessStatusCode) return;

        logger.LogInformation("Creating role '{Role}'...", name);
        using var create = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/roles");
        create.Content = JsonContent.Create(new { name, description = $"SeismicFlow {name} role" });
        (await httpClient.SendAsync(create, ct)).EnsureSuccessStatusCode();
    }

    // ── Protocol Mappers ──────────────────────────────────────────────────────

    private async Task EnsureTenantIdMapperAsync(CancellationToken ct)
    {
        var clientId = await GetClientInternalIdAsync("seismicflow-api", ct);
        if (clientId is null) return;
        if ((await GetClientMappersAsync(clientId, ct)).Any(m => m.Name == "tenant_id")) return;

        logger.LogInformation("Creating tenant_id mapper...");
        using var req = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/clients/{clientId}/protocol-mappers/models");
        req.Content = JsonContent.Create(new
        {
            name = "tenant_id",
            protocol = "openid-connect",
            protocolMapper = "oidc-usermodel-attribute-mapper",
            config = new Dictionary<string, string>
            {
                ["user.attribute"] = "tenant_id",
                ["claim.name"] = "tenant_id",
                ["jsonType.label"] = "String",
                ["id.token.claim"] = "true",
                ["access.token.claim"] = "true",
                ["userinfo.token.claim"] = "true"
            }
        });
        (await httpClient.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    private async Task EnsureAudienceMapperAsync(CancellationToken ct)
    {
        var clientId = await GetClientInternalIdAsync("seismicflow-api", ct);
        if (clientId is null) return;
        if ((await GetClientMappersAsync(clientId, ct)).Any(m => m.Name == "audience")) return;

        logger.LogInformation("Creating audience mapper...");
        using var req = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/clients/{clientId}/protocol-mappers/models");
        req.Content = JsonContent.Create(new
        {
            name = "audience",
            protocol = "openid-connect",
            protocolMapper = "oidc-audience-mapper",
            config = new Dictionary<string, string>
            {
                ["included.client.audience"] = "seismicflow-api",
                ["id.token.claim"] = "true",
                ["access.token.claim"] = "true"
            }
        });
        (await httpClient.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    private async Task EnsureRolesMapperAsync(CancellationToken ct)
    {
        var clientId = await GetClientInternalIdAsync("seismicflow-api", ct);
        if (clientId is null) return;
        if ((await GetClientMappersAsync(clientId, ct)).Any(m => m.Name == "realm-roles")) return;

        logger.LogInformation("Creating realm-roles mapper...");
        using var req = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/clients/{clientId}/protocol-mappers/models");
        req.Content = JsonContent.Create(new
        {
            name = "realm-roles",
            protocol = "openid-connect",
            protocolMapper = "oidc-usermodel-realm-role-mapper",
            config = new Dictionary<string, string>
            {
                ["claim.name"] = "roles",
                ["jsonType.label"] = "String",
                ["multivalued"] = "true",
                ["id.token.claim"] = "true",
                ["access.token.claim"] = "true",
                ["userinfo.token.claim"] = "true"
            }
        });
        (await httpClient.SendAsync(req, ct)).EnsureSuccessStatusCode();
    }

    // ── /tenants Group ────────────────────────────────────────────────────────

    private async Task EnsureTenantsGroupAsync(CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/groups?search=tenants&exact=true");
        var groups = await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<List<GroupResponse>>(cancellationToken: ct);
        if (groups?.Any(g => g.Path == "/tenants") == true) return;

        logger.LogInformation("Creating /tenants group...");
        using var create = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/groups");
        create.Content = JsonContent.Create(new { name = "tenants" });
        (await httpClient.SendAsync(create, ct)).EnsureSuccessStatusCode();
    }

    // ── User Profile: tenant_id attribute ────────────────────────────────────

    private async Task EnsureTenantIdUserProfileAttributeAsync(CancellationToken ct)
    {
        var url = $"{_opts.AdminBaseUrl}/users/profile";

        using var getReq = Auth(HttpMethod.Get, url);
        var getResponse = await httpClient.SendAsync(getReq, ct);
        if (!getResponse.IsSuccessStatusCode) return;

        var profile = await getResponse.Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken: ct);

        // Check if tenant_id already exists
        var attributes = profile.TryGetProperty("attributes", out var attrs)
            ? attrs.EnumerateArray().ToList()
            : new List<System.Text.Json.JsonElement>();

        if (attributes.Any(a =>
            a.TryGetProperty("name", out var n) && n.GetString() == "tenant_id"))
        {
            logger.LogDebug("tenant_id user profile attribute already exists.");
            return;
        }

        logger.LogInformation("Adding tenant_id to User Profile...");

        // Build updated profile with tenant_id added
        var updatedProfile = new
        {
            attributes = attributes
                .Select(a => (object)a)
                .Append(new
                {
                    name = "tenant_id",
                    displayName = "Tenant ID",
                    permissions = new { view = new[] { "admin", "user" }, edit = new[] { "admin" } },
                    multivalued = false
                })
                .ToArray()
        };

        using var putReq = Auth(HttpMethod.Put, url);
        putReq.Content = System.Net.Http.Json.JsonContent.Create(updatedProfile);
        var putResponse = await httpClient.SendAsync(putReq, ct);

        if (putResponse.IsSuccessStatusCode)
            logger.LogInformation("tenant_id user profile attribute added.");
        else
        {
            var body = await putResponse.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Failed to add tenant_id to user profile: {Body}", body);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> ClientExistsAsync(string clientId, CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/clients?clientId={clientId}");
        var clients = await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<List<ClientResponse>>(cancellationToken: ct);
        return clients?.Any(c => c.ClientId == clientId) == true;
    }

    public async Task<string?> GetClientInternalIdAsync(string clientId, CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/clients?clientId={clientId}");
        var clients = await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<List<ClientResponse>>(cancellationToken: ct);
        return clients?.FirstOrDefault(c => c.ClientId == clientId)?.Id;
    }

    private async Task<UserResponse?> GetServiceAccountUserAsync(CancellationToken ct)
    {
        var id = await GetClientInternalIdAsync(_opts.AdminClientId, ct);
        if (id is null) return null;
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/clients/{id}/service-account-user");
        return await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<UserResponse>(cancellationToken: ct);
    }

    private async Task<List<RoleResponse>> GetClientRolesAsync(
        string clientInternalId, string[] names, CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/clients/{clientInternalId}/roles");
        var roles = await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<List<RoleResponse>>(cancellationToken: ct);
        return roles?.Where(r => names.Contains(r.Name)).ToList() ?? [];
    }

    private async Task<List<MapperResponse>> GetClientMappersAsync(
        string clientInternalId, CancellationToken ct)
    {
        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/clients/{clientInternalId}/protocol-mappers/models");
        return await (await httpClient.SendAsync(req, ct))
            .Content.ReadFromJsonAsync<List<MapperResponse>>(cancellationToken: ct) ?? [];
    }

    private HttpRequestMessage Auth(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        return req;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")]
        string AccessToken);

    private sealed record ClientResponse(string Id, string ClientId);
    internal sealed record UserResponse(string Id, string Username);
    internal sealed record RoleResponse(string Id, string Name);
    private sealed record MapperResponse(string Id, string Name);
    private sealed record GroupResponse(string Id, string Name, string Path);
}
// NOTE: Add this method to the BootstrapAsync call and the class body