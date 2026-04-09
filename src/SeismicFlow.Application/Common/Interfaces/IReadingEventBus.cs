using System.Threading.Channels;
using SeismicFlow.Application.Mqtt;

namespace SeismicFlow.Application.Common.Interfaces;

/// <summary>
/// Lightweight in-process event bus.
/// MQTT consumer writes → SSE endpoint reads.
/// </summary>
public interface IReadingEventBus
{
    void Publish(ReadingReceivedEvent evt);
    IAsyncEnumerable<ReadingReceivedEvent> SubscribeAsync(
        Guid tenantId,
        Guid? deviceId,
        CancellationToken ct);
}

/// <summary>
/// Channel-based implementation — thread-safe, no external dependency.
/// One shared unbounded channel per (tenantId, deviceId) subscription.
/// </summary>
public sealed class ReadingEventBus : IReadingEventBus
{
    private readonly List<Subscription> _subs = [];
    private readonly Lock _lock = new();

    public void Publish(ReadingReceivedEvent evt)
    {
        List<Subscription> targets;
        lock (_lock)
        {
            // ⬇️ DEBUG
            Console.WriteLine($"[SSE-DEBUG] Publish: tenant={evt.TenantId}, device={evt.DeviceId}");
            Console.WriteLine($"[SSE-DEBUG] Active subscribers: {_subs.Count}");
            foreach (var s in _subs)
                Console.WriteLine($"[SSE-DEBUG]   sub: tenant={s.TenantId}, device={s.DeviceId}");

            targets = _subs
                .Where(s => s.TenantId == evt.TenantId &&
                            (s.DeviceId is null || s.DeviceId == evt.DeviceId))
                .ToList();

            Console.WriteLine($"[SSE-DEBUG] Matched: {targets.Count}");
        }

        foreach (var sub in targets)
            sub.Channel.Writer.TryWrite(evt);
    }

    public async IAsyncEnumerable<ReadingReceivedEvent> SubscribeAsync(
        Guid tenantId,
        Guid? deviceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var ch = Channel.CreateUnbounded<ReadingReceivedEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        var sub = new Subscription(tenantId, deviceId, ch);

        lock (_lock) _subs.Add(sub);

        try
        {
            await foreach (var evt in ch.Reader.ReadAllAsync(ct))
                yield return evt;
        }
        finally
        {
            lock (_lock) _subs.Remove(sub);
        }
    }

    private sealed record Subscription(
        Guid TenantId,
        Guid? DeviceId,
        Channel<ReadingReceivedEvent> Channel);
}