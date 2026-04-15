using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeismicFlow.Application.Common.DTOs;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Shared.Results;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SeismicFlow.Infrastructure.External.Keycloak;

public sealed class KeycloakUserService(
    HttpClient httpClient,
    KeycloakTokenService tokenService,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakUserService> logger) : IKeycloakUserService
{
    private readonly KeycloakOptions _opts = options.Value;

    // ── Create User ───────────────────────────────────────────────────────────

    public async Task<Result<string>> CreateUserAsync(
        CreateUserRequest request, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return Error.ExternalService("Keycloak", "Could not obtain admin token.");

        // Create user
        using var req = Auth(HttpMethod.Post, $"{_opts.AdminBaseUrl}/users", token);
        req.Content = JsonContent.Create(new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new { type = "password", value = request.Password, temporary = false }
            },
            attributes = new Dictionary<string, string[]>
            {
                ["tenant_id"] = [request.TenantId.ToString()]
            }
        });

        var response = await httpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Failed to create user '{Username}': {Body}", request.Username, body);
            return Error.ExternalService("Keycloak", $"Failed to create user: {body}");
        }

        // Extract user ID from Location header
        var userId = response.Headers.Location?.ToString().Split('/').Last();
        if (string.IsNullOrEmpty(userId))
            return Error.ExternalService("Keycloak", "No user ID returned.");

        logger.LogInformation("User '{Username}' created with ID {UserId}.", request.Username, userId);

        // Assign role
        var roleResult = await AssignRoleAsync(userId, request.Role, token, ct);
        if (roleResult.IsFailure)
            logger.LogWarning("Role assignment failed for '{Username}': {Error}", request.Username, roleResult.Error);

        // Add to tenant group
        var groupResult = await AddToTenantGroupAsync(userId, request.TenantId, token, ct);
        if (groupResult.IsFailure)
            logger.LogWarning("Group assignment failed for '{Username}': {Error}", request.Username, groupResult.Error);

        return userId;
    }

    // ── Assign Role ───────────────────────────────────────────────────────────

    public async Task<Result> AssignRoleAsync(
        string userId, string roleName, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return Error.ExternalService("Keycloak", "Could not obtain admin token.");
        return await AssignRoleAsync(userId, roleName, token, ct);
    }

    private async Task<Result> AssignRoleAsync(
        string userId, string roleName, string token, CancellationToken ct)
    {
        // Get role details
        using var roleReq = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/roles/{roleName}", token);
        var roleResponse = await httpClient.SendAsync(roleReq, ct);
        if (!roleResponse.IsSuccessStatusCode)
            return Error.ExternalService("Keycloak", $"Role '{roleName}' not found.");

        var role = await roleResponse.Content
            .ReadFromJsonAsync<RoleDto>(cancellationToken: ct);
        if (role is null)
            return Error.ExternalService("Keycloak", "Null role response.");

        // Assign role to user
        using var assignReq = Auth(HttpMethod.Post,
            $"{_opts.AdminBaseUrl}/users/{userId}/role-mappings/realm", token);
        assignReq.Content = JsonContent.Create(new[] { new { id = role.Id, name = role.Name } });

        var response = await httpClient.SendAsync(assignReq, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return Error.ExternalService("Keycloak", $"Failed to assign role: {body}");
        }

        logger.LogInformation("Role '{Role}' assigned to user '{UserId}'.", roleName, userId);
        return Result.Success();
    }

    // ── Update Tenant ─────────────────────────────────────────────────────────

    public async Task<Result> UpdateUserTenantAsync(
        string userId, Guid tenantId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return Error.ExternalService("Keycloak", "Could not obtain admin token.");

        using var req = Auth(HttpMethod.Put,
            $"{_opts.AdminBaseUrl}/users/{userId}", token);
        req.Content = JsonContent.Create(new
        {
            attributes = new Dictionary<string, string[]>
            {
                ["tenant_id"] = [tenantId.ToString()]
            }
        });

        var response = await httpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return Error.ExternalService("Keycloak", $"Failed to update tenant: {body}");
        }

        return Result.Success();
    }

    // ── Delete User ───────────────────────────────────────────────────────────

    public async Task<Result> DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return Error.ExternalService("Keycloak", "Could not obtain admin token.");

        using var req = Auth(HttpMethod.Delete,
            $"{_opts.AdminBaseUrl}/users/{userId}", token);
        var response = await httpClient.SendAsync(req, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return Error.ExternalService("Keycloak", $"Failed to delete user: {body}");
        }

        logger.LogInformation("User '{UserId}' deleted.", userId);
        return Result.Success();
    }

    // ── Get Tenant Users ──────────────────────────────────────────────────────

    public async Task<Result<List<KeycloakUserDto>>> GetTenantUsersAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        if (token is null) return Error.ExternalService("Keycloak", "Could not obtain admin token.");

        using var req = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/users?q=tenant_id:{tenantId}&max=100", token);
        var response = await httpClient.SendAsync(req, ct);

        if (!response.IsSuccessStatusCode)
            return Error.ExternalService("Keycloak", "Failed to list users.");

        var users = await response.Content
            .ReadFromJsonAsync<List<KeycloakUserDto>>(cancellationToken: ct);
        return users ?? [];
    }

    // ── Add to Tenant Group ───────────────────────────────────────────────────

    private async Task<Result> AddToTenantGroupAsync(
        string userId, Guid tenantId, string token, CancellationToken ct)
    {
        // Find tenant group
        using var searchReq = Auth(HttpMethod.Get,
            $"{_opts.AdminBaseUrl}/groups?search={tenantId}&max=10", token);
        var groups = await (await httpClient.SendAsync(searchReq, ct))
            .Content.ReadFromJsonAsync<List<GroupDto>>(cancellationToken: ct);

        var group = groups?.FirstOrDefault(g => g.Path.Contains(tenantId.ToString()));
        if (group is null)
        {
            logger.LogWarning("Tenant group not found for {TenantId}, skipping.", tenantId);
            return Result.Success();
        }

        using var req = Auth(HttpMethod.Put,
            $"{_opts.AdminBaseUrl}/users/{userId}/groups/{group.Id}", token);
        await httpClient.SendAsync(req, ct);
        return Result.Success();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        var result = await tokenService.GetTokenAsync(ct);
        return result.IsSuccess ? result.Value : null;
    }

    private static HttpRequestMessage Auth(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private sealed record RoleDto(string Id, string Name);
    private sealed record GroupDto(string Id, string Name, string Path);
}

