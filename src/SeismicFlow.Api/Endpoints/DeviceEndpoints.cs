using MediatR;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Devices.Commands;
using SeismicFlow.Application.Devices.Queries;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Api.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/devices")
            .WithTags("Devices")
            .RequireAuthorization("DeviceRead");

        group.MapPost("/", RegisterDevice)
            .WithName("RegisterDevice")
            .WithSummary("Register a new device for the current tenant.")
            .RequireAuthorization("DeviceWrite");

        group.MapGet("/", GetDevices)
            .WithName("GetDevices")
            .WithSummary("Get all devices for the current tenant.");

        group.MapGet("/{id:guid}", GetDeviceById)
            .WithName("GetDeviceById")
            .WithSummary("Get a device by ID.");

        group.MapPatch("/{id:guid}/deactivate", DeactivateDevice)
            .WithName("DeactivateDevice")
            .WithSummary("Deactivate a device. This action cannot be undone.")
            .RequireAuthorization("DeviceWrite");

        group.MapGet("/{id:guid}/mqtt-credentials", GetDeviceMqttCredentials)
            .WithName("GetDeviceMqttCredentials")
            .WithSummary("Get MQTT credentials for a device.")
            .RequireAuthorization("DeviceWrite");
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterDevice(
        RegisterDeviceRequest request,
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        // Super-admin has no tenant_id in JWT — must provide it in the request body.
        // Regular users always use the tenant from their JWT/middleware.
        var tenantId = ResolveTenantId(context, request.TenantId);
        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required. Super-admin must specify tenantId in the request body." });

        var command = new RegisterDeviceCommand(
            request.SerialNumber,
            tenantId.Value,
            request.Name,
            request.Latitude,
            request.Longitude,
            request.Altitude,
            request.NetworkCode,
            request.StationCode);

        var result = await sender.Send(command, ct);

        return result.Match(
            onSuccess: device => Results.Created($"/api/v1/devices/{device.Id}", device),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> GetDevices(
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        var tenantId = context.GetTenantIdOrNull();
        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required. Use X-Tenant-Id header." });

        var result = await sender.Send(new GetDevicesByTenantQuery(tenantId.Value), ct);

        return result.Match(
            onSuccess: devices => Results.Ok(devices),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> GetDeviceById(
        Guid id,
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        var tenantId = context.GetTenantIdOrNull();
        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required. Use X-Tenant-Id header." });

        var result = await sender.Send(new GetDeviceByIdQuery(id, tenantId.Value), ct);

        return result.Match(
            onSuccess: device => Results.Ok(device),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> DeactivateDevice(
        Guid id,
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        var tenantId = context.GetTenantIdOrNull();
        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required. Use X-Tenant-Id header." });

        var result = await sender.Send(new DeactivateDeviceCommand(id, tenantId.Value), ct);

        return result.Match(
            onSuccess: device => Results.Ok(device),
            onFailure: error => MapError(error));
    }

    private static async Task<IResult> GetDeviceMqttCredentials(
        Guid id,
        HttpContext context,
        ISender sender,
        CancellationToken ct)
    {
        var tenantId = context.GetTenantIdOrNull();
        if (tenantId is null)
            return Results.BadRequest(new { error = "TenantId is required. Use X-Tenant-Id header." });

        var result = await sender.Send(new GetDeviceMqttCredentialsQuery(id, tenantId.Value), ct);

        return result.Match(
            onSuccess: dto => Results.Ok(dto),
            onFailure: error => MapError(error));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves tenantId: for super-admin, uses the value from the request body;
    /// for regular users, uses the value set by TenantMiddleware.
    /// </summary>
    private static Guid? ResolveTenantId(HttpContext context, Guid? requestTenantId)
    {
        var isSuperAdmin = context.User.IsInRole("super-admin");

        if (isSuperAdmin)
            return requestTenantId ?? context.GetTenantIdOrNull();

        return context.GetTenantIdOrNull();
    }

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

public sealed record RegisterDeviceRequest(
    string SerialNumber,
    string Name,
    double Latitude,
    double Longitude,
    double Altitude,
    string NetworkCode,
    string StationCode,
    Guid? TenantId = null);