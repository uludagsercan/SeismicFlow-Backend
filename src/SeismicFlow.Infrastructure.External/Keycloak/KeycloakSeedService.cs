using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SeismicFlow.Infrastructure.External.Keycloak;

/// <summary>
/// Seeds initial data into Keycloak after bootstrap.
/// Creates the super-admin user if it doesn't exist.
/// Idempotent — safe to run multiple times.
/// </summary>
public sealed class KeycloakSeedService(
    HttpClient httpClient,
    KeycloakTokenService tokenService,
    IOptions<KeycloakOptions> options,
    IOptions<SeedOptions> seedOptions,
    ILogger<KeycloakSeedService> logger)
{
    private readonly KeycloakOptions _opts = options.Value;
    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!_seed.Enabled)
        {
            logger.LogDebug("Seeding is disabled, skipping.");
            return;
        }

        logger.LogInformation("Starting Keycloak seed...");

        try
        {
            var tokenResult = await tokenService.GetTokenAsync(ct);
            if (tokenResult.IsFailure)
            {
                logger.LogError("Could not get admin token for seeding.");
                return;
            }
            var token = tokenResult.Value!;

            await EnsureSuperAdminAsync(token, ct);

            logger.LogInformation("Keycloak seed completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak seed failed.");
        }
    }

    private async Task EnsureSuperAdminAsync(string token, CancellationToken ct)
    {
        // Check if user already exists
        using var searchReq = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/users?username={_seed.SuperAdminUsername}&exact=true", token);
        var searchResponse = await httpClient.SendAsync(searchReq, ct);
        var existing = await searchResponse.Content
            .ReadFromJsonAsync<List<UserResponse>>(cancellationToken: ct);

        if (existing?.Count > 0)
        {
            logger.LogDebug("Super-admin '{Username}' already exists.", _seed.SuperAdminUsername);
            return;
        }

        logger.LogInformation("Creating super-admin user '{Username}'...", _seed.SuperAdminUsername);

        // Create user
        using var createReq = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/users", token);
        createReq.Content = JsonContent.Create(new
        {
            username = _seed.SuperAdminUsername,
            email = _seed.SuperAdminEmail,
            firstName = "Super",
            lastName = "Admin",
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new
                {
                    type      = "password",
                    value     = _seed.SuperAdminPassword,
                    temporary = false
                }
            },
            attributes = new Dictionary<string, string[]>
            {
                // super-admin has no tenant — use a fixed system UUID
                ["tenant_id"] = [_seed.SystemTenantId]
            }
        });

        var createResponse = await httpClient.SendAsync(createReq, ct);
        if (!createResponse.IsSuccessStatusCode)
        {
            var body = await createResponse.Content.ReadAsStringAsync(ct);
            logger.LogError("Failed to create super-admin: {Body}", body);
            return;
        }

        var userId = createResponse.Headers.Location?.ToString().Split('/').Last();
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogError("No user ID returned for super-admin.");
            return;
        }

        logger.LogInformation("Super-admin created with ID {UserId}.", userId);

        // Assign super-admin role
        await AssignRoleAsync(userId, "super-admin", token, ct);

        logger.LogInformation("Super-admin '{Username}' seeded successfully.", _seed.SuperAdminUsername);
    }

    private async Task AssignRoleAsync(string userId, string roleName, string token, CancellationToken ct)
    {
        // Get role
        using var roleReq = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/roles/{roleName}", token);
        var roleResponse = await httpClient.SendAsync(roleReq, ct);
        if (!roleResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Role '{Role}' not found.", roleName);
            return;
        }

        var role = await roleResponse.Content
            .ReadFromJsonAsync<RoleResponse>(cancellationToken: ct);
        if (role is null) return;

        // Assign
        using var assignReq = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/users/{userId}/role-mappings/realm", token);
        assignReq.Content = JsonContent.Create(
            new[] { new { id = role.Id, name = role.Name } });

        await httpClient.SendAsync(assignReq, ct);
        logger.LogInformation("Role '{Role}' assigned to user '{UserId}'.", roleName, userId);
    }

    private static HttpRequestMessage Auth(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private sealed record UserResponse(string Id, string Username);
    private sealed record RoleResponse(string Id, string Name);
}

// ── Seed Options ──────────────────────────────────────────────────────────────

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public bool Enabled { get; set; } = true;
    public string SuperAdminUsername { get; set; } = "superadmin";
    public string SuperAdminEmail { get; set; } = "superadmin@seismicflow.io";
    public string SuperAdminPassword { get; set; } = "Admin123!";

    /// <summary>
    /// Fixed UUID used as tenant_id for super-admin.
    /// Super-admin is not bound to any tenant.
    /// </summary>
    public string SystemTenantId { get; set; } = "00000000-0000-0000-0000-000000000001";
}