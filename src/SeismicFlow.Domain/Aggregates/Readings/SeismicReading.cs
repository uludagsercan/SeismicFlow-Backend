namespace SeismicFlow.Domain.Aggregates.Readings;

/// <summary>
/// Represents a single accelerometer reading ingested from a device via MQTT.
/// Value object — immutable after creation, no business behaviour.
/// </summary>
public sealed class SeismicReading
{
    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }

    /// <summary>Channel code: BHN | BHE | BHZ</summary>
    public string Channel { get; private set; } = default!;

    /// <summary>UTC epoch of the first sample in this record.</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Sample rate in Hz.</summary>
    public double SampleRate { get; private set; }

    /// <summary>Raw sample values (JSON-serialised as a double array).</summary>
    public double[] Samples { get; private set; } = [];

    private SeismicReading() { }

    public static SeismicReading Create(
        Guid deviceId,
        string channel,
        DateTimeOffset timestamp,
        double sampleRate,
        double[] samples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentNullException.ThrowIfNull(samples);

        return new SeismicReading
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Channel = channel,
            Timestamp = timestamp,
            SampleRate = sampleRate,
            Samples = samples
        };
    }
}