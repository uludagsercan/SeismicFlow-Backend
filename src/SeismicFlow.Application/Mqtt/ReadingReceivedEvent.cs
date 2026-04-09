namespace SeismicFlow.Application.Mqtt;

/// <summary>
/// In-process event published after a reading is persisted.
/// Consumed by the SSE broadcaster.
/// </summary>
public sealed record ReadingReceivedEvent(
    Guid TenantId,
    Guid DeviceId,
    string Channel,
    DateTimeOffset Timestamp,
    double SampleRate,
    double[] Samples
);