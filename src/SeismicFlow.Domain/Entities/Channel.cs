namespace SeismicFlow.Domain.Entities;

/// <summary>
/// Represents a data channel discovered from a Device via MQTT.
/// Channels are auto-created when first seen; lastSeenAt is updated on each reading.
/// </summary>
public sealed class Channel
{
    public Guid Id { get; private set; }

    /// <summary>Free-form SEED channel code, e.g. "BHZ", "HHN", "LHZ".</summary>
    public string Code { get; private set; } = default!;

    public double SampleRate { get; private set; }
    public DateTimeOffset FirstSeenAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    // EF Core
    private Channel() { }

    internal static Channel Create(string code, double sampleRate) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        SampleRate = sampleRate,
        FirstSeenAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };

    internal void UpdateLastSeen(double sampleRate)
    {
        SampleRate = sampleRate;
        LastSeenAt = DateTimeOffset.UtcNow;
    }
}