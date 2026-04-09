using Microsoft.Extensions.Options;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Common.DTOs;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Infrastructure.External.Keycloak;
using System.Net.Http.Json;

namespace SeismicFlow.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithSummary("Register a new user and assign to tenant")
            .RequireAuthorization("TenantAdmin");

        group.MapPost("/login", LoginAsync)
            .WithSummary("Login and get JWT token")
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshAsync)
            .WithSummary("Refresh JWT token")
            .AllowAnonymous();
    }

    // ── Register ──────────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IKeycloakUserService userService,
        HttpContext context,
        CancellationToken ct)
    {
        // Only super-admin can register without tenant context
        var isSuperAdmin = context.User.IsInRole("super-admin");
        var tenantId = isSuperAdmin && request.TenantId.HasValue
            ? request.TenantId.Value
            : context.GetTenantIdOrNull();

        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required." });

        var role = request.Role ?? "tenant-user";

        // Only super-admin can assign super-admin role
        if (role == "super-admin" && !isSuperAdmin)
            return Results.Forbid();

        var result = await userService.CreateUserAsync(new CreateUserRequest(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            role,
            tenantId.Value), ct);

        if (result.IsFailure)
            return Results.Problem(result.Error!.Message, statusCode: 502);

        return Results.Ok(new { userId = result.Value, username = request.Username, role, tenantId });
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IOptions<KeycloakOptions> options,
        HttpClient httpClient,
        CancellationToken ct)
    {
        var opts = options.Value;
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "seismicflow-api",
            ["username"] = request.Username,
            ["password"] = request.Password
        });

        var response = await httpClient.PostAsync(opts.TokenUrl, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            return Results.Problem(err, statusCode: (int)response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
        return Results.Ok(token);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        IOptions<KeycloakOptions> options,
        HttpClient httpClient,
        CancellationToken ct)
    {
        var opts = options.Value;
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "seismicflow-api",
            ["refresh_token"] = request.RefreshToken
        });

        var response = await httpClient.PostAsync(opts.TokenUrl, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            return Results.Problem(err, statusCode: (int)response.StatusCode);
        }

        var token = await response.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
        return Results.Ok(token);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record RegisterRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? Role,
    Guid? TenantId);

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);