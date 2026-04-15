using Microsoft.EntityFrameworkCore;
using SeismicFlow.Api.Middleware;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Api.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/devices/{deviceId:guid}/channels", GetChannels)
            .WithTags("Channels")
            .WithName("GetDeviceChannels")
            .WithSummary("List auto-discovered channels for a device.")
            .RequireAuthorization("DeviceRead");
    }

    private static async Task<IResult> GetChannels(
        Guid deviceId,
        HttpContext ctx,
        ITenantDbContextFactory factory,
        CancellationToken ct)
    {
        var tenantId = ctx.GetTenantId();
        var db = await factory.CreateAsync(tenantId, ct);

        var device = await db.Devices
            .Include(d => d.Channels)
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct);

        if (device is null)
            return Results.NotFound();

        var channels = device.Channels.Select(c => new
        {
            code = c.Code,
            sampleRate = c.SampleRate,
            firstSeenAt = c.FirstSeenAt,
            lastSeenAt = c.LastSeenAt,
        });

        return Results.Ok(channels);
    }
}