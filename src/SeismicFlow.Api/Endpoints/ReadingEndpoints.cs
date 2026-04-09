using Microsoft.AspNetCore.Mvc;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Application.Common.Interfaces;
using SeismicFlow.Application.Readings.DTOs;
using SeismicFlow.Infrastructure.Persistence.Contexts;
using SeismicFlow.Infrastructure.Persistence.Repositories;
using System.Text.Json;

namespace SeismicFlow.Api.Endpoints;

public static class ReadingEndpoints
{
    public static void MapReadingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/devices/{deviceId:guid}/readings")
            .WithTags("Readings")
            .RequireAuthorization("DeviceRead");

        // GET /api/v1/devices/{deviceId}/readings
        group.MapGet("/", GetReadings)
            .WithName("GetReadings")
            .WithSummary("Query historical readings for a device.");

        // GET /api/v1/devices/{deviceId}/readings/latest
        group.MapGet("/latest", GetLatestReading)
            .WithName("GetLatestReading")
            .WithSummary("Get the most recent reading for a device.");

        // GET /api/v1/devices/{deviceId}/readings/stream  (SSE)
        // EventSource (browser) cannot send Authorization header, so we also accept ?access_token=
        group.MapGet("/stream", StreamReadings)
            .WithName("StreamReadings")
            .WithSummary("Real-time SSE stream of incoming readings for a device.")
            .AddEndpointFilter(async (ctx, next) =>
            {
                // If Authorization header is missing, try query string token
                if (!ctx.HttpContext.Request.Headers.ContainsKey("Authorization"))
                {
                    var token = ctx.HttpContext.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(token))
                        ctx.HttpContext.Request.Headers.Authorization = $"Bearer {token}";
                }
                return await next(ctx);
            });
    }

    // ── Historical query ──────────────────────────────────────────────────────

    private static async Task<IResult> GetReadings(
        Guid deviceId,
        HttpContext ctx,
        SeismicReadingRepository repo,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? channel,
        [FromQuery] int limit = 1000,
        CancellationToken ct = default)
    {
        var tenantId = ctx.GetTenantId();

        var readings = await repo.GetByDeviceAsync(
            tenantId, deviceId,
            from ?? DateTimeOffset.UtcNow.AddHours(-1),
            to ?? DateTimeOffset.UtcNow,
            channel, Math.Clamp(limit, 1, 10_000), ct);

        var dtos = readings.Select(r => new SeismicReadingDto(
            r.Id, r.DeviceId, r.Channel, r.Timestamp,
            r.SampleRate, r.Samples.Length, r.Samples)).ToList();

        return Results.Ok(new ReadingPageDto(dtos, dtos.Count));
    }

    private static async Task<IResult> GetLatestReading(
        Guid deviceId,
        HttpContext ctx,
        SeismicReadingRepository repo,
        [FromQuery] string? channel,
        CancellationToken ct = default)
    {
        var tenantId = ctx.GetTenantId();

        var r = await repo.GetLatestByDeviceAsync(tenantId, deviceId, channel, ct);
        if (r is null) return Results.NotFound();

        return Results.Ok(new SeismicReadingDto(
            r.Id, r.DeviceId, r.Channel, r.Timestamp,
            r.SampleRate, r.Samples.Length, r.Samples));
    }

    // ── SSE stream ────────────────────────────────────────────────────────────

    private static async Task StreamReadings(
        Guid deviceId,
        HttpContext ctx,
        IReadingEventBus eventBus,
        [FromQuery] string? channel,
        CancellationToken ct)
    {
        var tenantId = ctx.GetTenantId();

        ctx.Response.StatusCode = 200;
        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";
        ctx.Response.Headers["X-Accel-Buffering"] = "no"; // Nginx proxy buffering'i devre dışı bırak

        await ctx.Response.WriteAsync("retry: 3000\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        await foreach (var evt in eventBus.SubscribeAsync(tenantId, deviceId, ct))
        {
            // Filter by channel if requested
            if (!string.IsNullOrEmpty(channel) &&
                !string.Equals(evt.Channel, channel, StringComparison.OrdinalIgnoreCase))
                continue;

            var data = JsonSerializer.Serialize(new
            {
                deviceId = evt.DeviceId,
                channel = evt.Channel,
                timestamp = evt.Timestamp.ToUnixTimeMilliseconds(),
                sampleRate = evt.SampleRate,
                samples = evt.Samples
            });

            await ctx.Response.WriteAsync($"data: {data}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
}