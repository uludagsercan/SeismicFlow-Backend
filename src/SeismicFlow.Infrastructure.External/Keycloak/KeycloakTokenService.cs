using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeismicFlow.Shared.Results;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SeismicFlow.Infrastructure.External.Keycloak
{
    /// <summary>
    /// Handles Keycloak admin token acquisition via client_credentials grant.
    /// Token is cached in memory until it expires (with a 30 second buffer).
    /// Single responsibility: only token management, nothing else.
    /// </summary>
    public sealed class KeycloakTokenService(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakTokenService> logger)
    {
        private readonly KeycloakOptions _opts = options.Value;

        private string? _cachedToken;
        private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

        public async Task<Result<string>> GetTokenAsync(CancellationToken ct = default)
        {
            // Return cached token if still valid (30 second buffer)
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry.AddSeconds(-30))
                return _cachedToken;

            logger.LogDebug("Fetching new Keycloak admin token for realm: {Realm}", _opts.Realm);

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _opts.AdminClientId,
                ["client_secret"] = _opts.AdminClientSecret,
            };

            try
            {
                var response = await httpClient.PostAsync(
                    _opts.TokenUrl,
                    new FormUrlEncodedContent(formData),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    return Error.ExternalService("Keycloak",
                        $"Failed to obtain admin token: {(int)response.StatusCode} {body}");
                }

                var tokenResponse = await response.Content
                    .ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct);

                _cachedToken = tokenResponse!.AccessToken;
                _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                return _cachedToken;
            }
            catch (HttpRequestException ex)
            {
                return Error.ExternalService("Keycloak",
                    $"Network error while obtaining admin token: {ex.Message}");
            }
        }
    }

    // ── Internal response DTOs ────────────────────────────────────────────────────

    internal sealed class KeycloakTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = default!;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    internal sealed class KeycloakGroupResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("path")]
        public string Path { get; set; } = default!;
    }
}
