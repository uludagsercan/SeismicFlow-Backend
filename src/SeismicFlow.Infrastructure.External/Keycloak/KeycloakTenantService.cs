using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Shared.Results;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SeismicFlow.Infrastructure.External.Keycloak;

public sealed class KeycloakTenantService(
    HttpClient httpClient,
    KeycloakTokenService tokenService,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakTenantService> logger)
    : IKeycloakTenantService
{
    private readonly KeycloakOptions _opts = options.Value;

    public async Task<Result<(string GroupId, string GroupPath)>> CreateTenantGroupAsync(
        TenantSlug slug, CancellationToken ct = default)
    {
        logger.LogInformation("Creating Keycloak group for tenant: {Slug}", slug.Value);

        try
        {
            var tokenResult = await tokenService.GetTokenAsync(ct);
            if (tokenResult.IsFailure)
            {
                logger.LogWarning("Could not get admin token, skipping Keycloak group creation.");
                return (Guid.NewGuid().ToString(), $"/tenants/{slug.Value}");
            }
            var token = tokenResult.Value!;

            var parentResult = await EnsureParentGroupAsync(token, ct);
            if (parentResult.IsFailure)
            {
                logger.LogWarning("Could not ensure parent group, skipping: {Error}", parentResult.Error);
                return (Guid.NewGuid().ToString(), $"/tenants/{slug.Value}");
            }
            var parentGroupId = parentResult.Value!;

            var groupResult = await CreateChildGroupAsync(token, parentGroupId, slug.Value, ct);
            if (groupResult.IsFailure)
            {
                logger.LogWarning("Could not create child group, skipping: {Error}", groupResult.Error);
                return (Guid.NewGuid().ToString(), $"/tenants/{slug.Value}");
            }
            var groupId = groupResult.Value!;

            var fetchResult = await GetGroupByIdAsync(token, groupId, ct);
            if (fetchResult.IsFailure)
                return (groupId, $"/tenants/{slug.Value}");

            var group = fetchResult.Value!;
            return (group.Id, group.Path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Keycloak group creation failed, continuing without it.");
            return (Guid.NewGuid().ToString(), $"/tenants/{slug.Value}");
        }
    }

    private async Task<Result<string>> EnsureParentGroupAsync(string token, CancellationToken ct)
    {
        var url = $"{_opts.AdminBaseUrl}/groups?search=tenants&exact=true";
        using var request = BuildRequest(HttpMethod.Get, url, token);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return Error.ExternalService("Keycloak", "Failed to search for parent group.");

        var groups = await response.Content
            .ReadFromJsonAsync<List<KeycloakGroupResponse>>(cancellationToken: ct);

        var parent = groups?.FirstOrDefault(g => g.Path == _opts.TenantGroupParentPath);
        if (parent is not null)
            return parent.Id;

        return await CreateTopLevelGroupAsync(token, "tenants", ct);
    }

    private async Task<Result<string>> CreateTopLevelGroupAsync(
        string token, string name, CancellationToken ct)
    {
        var url = $"{_opts.AdminBaseUrl}/groups";
        using var request = BuildRequest(HttpMethod.Post, url, token);
        request.Content = JsonContent.Create(new { name });

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return Error.ExternalService("Keycloak", $"Failed to create group: {body}");
        }

        return ExtractIdFromLocation(response);
    }

    private async Task<Result<string>> CreateChildGroupAsync(
        string token, string parentGroupId, string name, CancellationToken ct)
    {
        var url = $"{_opts.AdminBaseUrl}/groups/{parentGroupId}/children";
        using var request = BuildRequest(HttpMethod.Post, url, token);
        request.Content = JsonContent.Create(new { name });

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return Error.ExternalService("Keycloak", $"Failed to create child group: {body}");
        }

        return ExtractIdFromLocation(response);
    }

    private async Task<Result<KeycloakGroupResponse>> GetGroupByIdAsync(
        string token, string groupId, CancellationToken ct)
    {
        var url = $"{_opts.AdminBaseUrl}/groups/{groupId}";
        using var request = BuildRequest(HttpMethod.Get, url, token);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return Error.ExternalService("Keycloak", $"Failed to fetch group {groupId}.");

        var group = await response.Content
            .ReadFromJsonAsync<KeycloakGroupResponse>(cancellationToken: ct);

        return group is null
            ? Error.ExternalService("Keycloak", "Null response.")
            : group;
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Result<string> ExtractIdFromLocation(HttpResponseMessage response)
    {
        var location = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(location))
            return Error.ExternalService("Keycloak", "No Location header.");
        return location.Split('/').Last();
    }
}

