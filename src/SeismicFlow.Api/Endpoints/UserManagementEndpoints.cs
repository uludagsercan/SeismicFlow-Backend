using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Common.DTOs;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Infrastructure.External.Keycloak;

namespace SeismicFlow.Api.Endpoints;

public static class UserManagementEndpoints
{
    public static void MapUserManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tenants/{tenantId}/users")
            .WithTags("User Management")
            .RequireAuthorization("TenantAdmin");

        group.MapGet("/", GetUsersAsync)
            .WithSummary("List all users in a tenant");

        group.MapPost("/", AddUserAsync)
            .WithSummary("Add a new user to tenant");

        group.MapDelete("/{userId}", DeleteUserAsync)
            .WithSummary("Remove user from tenant");

        group.MapPut("/{userId}/roles", UpdateUserRoleAsync)
            .WithSummary("Update user role");
    }

    // ── GET /api/v1/tenants/{tenantId}/users ──────────────────────────────────

    private static async Task<IResult> GetUsersAsync(
        Guid tenantId,
        IKeycloakUserService userService,
        HttpContext context,
        CancellationToken ct)
    {
        if (!CanAccessTenant(context, tenantId))
            return Results.Forbid();

        var result = await userService.GetTenantUsersAsync(tenantId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error!.Message, statusCode: 502);
    }

    // ── POST /api/v1/tenants/{tenantId}/users ─────────────────────────────────

    private static async Task<IResult> AddUserAsync(
        Guid tenantId,
        AddUserRequest request,
        IKeycloakUserService userService,
        HttpContext context,
        CancellationToken ct)
    {
        if (!CanAccessTenant(context, tenantId))
            return Results.Forbid();

        var role = request.Role ?? "tenant-user";

        // Only super-admin can assign super-admin role
        if (role == "super-admin" && !context.User.IsInRole("super-admin"))
            return Results.Forbid();

        var result = await userService.CreateUserAsync(new CreateUserRequest(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            role,
            tenantId), ct);

        return result.IsSuccess
            ? Results.Ok(new { userId = result.Value, username = request.Username, role, tenantId })
            : Results.Problem(result.Error!.Message, statusCode: 502);
    }

    // ── DELETE /api/v1/tenants/{tenantId}/users/{userId} ──────────────────────

    private static async Task<IResult> DeleteUserAsync(
        Guid tenantId,
        string userId,
        IKeycloakUserService userService,
        HttpContext context,
        CancellationToken ct)
    {
        if (!CanAccessTenant(context, tenantId))
            return Results.Forbid();

        var result = await userService.DeleteUserAsync(userId, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.Problem(result.Error!.Message, statusCode: 502);
    }

    // ── PUT /api/v1/tenants/{tenantId}/users/{userId}/roles ───────────────────

    private static async Task<IResult> UpdateUserRoleAsync(
        Guid tenantId,
        string userId,
        UpdateRoleRequest request,
        IKeycloakUserService userService,
        HttpContext context,
        CancellationToken ct)
    {
        if (!CanAccessTenant(context, tenantId))
            return Results.Forbid();

        if (request.Role == "super-admin" && !context.User.IsInRole("super-admin"))
            return Results.Forbid();

        var result = await userService.AssignRoleAsync(userId, request.Role, ct);
        return result.IsSuccess
            ? Results.Ok(new { userId, role = request.Role })
            : Results.Problem(result.Error!.Message, statusCode: 502);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool CanAccessTenant(HttpContext context, Guid tenantId)
    {
        // super-admin can access any tenant
        if (context.User.IsInRole("super-admin")) return true;

        // tenant-admin can only access their own tenant
        var userTenantId = context.GetTenantIdOrNull();
        return userTenantId == tenantId;
    }
}

public sealed record AddUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string? Role);

public sealed record UpdateRoleRequest(string Role);