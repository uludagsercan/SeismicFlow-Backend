using MediatR;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Tenants.Commands;
using SeismicFlow.Application.Tenants.Queries;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants")
            .WithTags("Tenants");

        group.MapPost("/", CreateTenant)
            .WithName("CreateTenant")
            .WithSummary("Create a new tenant and provision its database and Keycloak group.")
            .RequireAuthorization("SuperAdmin");

        group.MapGet("/", GetAllTenants)
            .WithName("GetAllTenants")
            .WithSummary("List all tenants (super-admin only).")
            .RequireAuthorization("SuperAdmin");

        group.MapGet("/{id:guid}", GetTenantById)
            .WithName("GetTenantById")
            .WithSummary("Get tenant by ID.")
            .RequireAuthorization("TenantAdmin");
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var command = new CreateTenantCommand(
            request.Slug,
            request.DisplayName,
            request.DbHost,
            request.DbPort);

        var result = await sender.Send(command, ct);

        return result.Match(
            onSuccess: tenant => Results.Created($"/api/v1/tenants/{tenant.Id}", tenant),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> GetAllTenants(
        ISender sender,
        CancellationToken ct)
    {
        // Reuse existing query — returns all tenants
        // Only super-admin can call this (enforced by policy)
        var result = await sender.Send(new GetAllTenantsQuery(), ct);

        return result.Match(
            onSuccess: tenants => Results.Ok(tenants),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> GetTenantById(
        Guid id,
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        // Tenant-admin can only view their own tenant
        if (!context.User.IsInRole("super-admin"))
        {
            var userTenantId = context.GetTenantIdOrNull();
            if (userTenantId != id)
                return Results.Forbid();
        }

        var result = await sender.Send(new GetTenantByIdQuery(id), ct);

        return result.Match(
            onSuccess: tenant => Results.Ok(tenant),
            onFailure: error => MapError(error));
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    private static IResult MapError(Error error) => error.Code switch
    {
        var c when c.EndsWith(".NotFound") => Results.NotFound(new { error.Code, error.Message }),
        var c when c.EndsWith(".Conflict") => Results.Conflict(new { error.Code, error.Message }),
        "Validation.Failed" => Results.BadRequest(new { error.Code, error.Message }),
        "InvalidOperation" => Results.BadRequest(new { error.Code, error.Message }),
        _ => Results.Problem(error.Message, statusCode: 502)
    };
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public sealed record CreateTenantRequest(
    string Slug,
    string DisplayName,
    string DbHost,
    int DbPort = 5432);