namespace SeismicFlow.Application.Mqtt;

/// <summary>
/// JSON payload published by a device to its MQTT topic.
/// Topic: tenant/{tenantId}/devices/{deviceId}/data
/// </summary>
public sealed record MqttReadingPayload(
    string Channel,
    long TimestampMs,   // Unix epoch ms (UTC)
    double SampleRate,
    double[] Samples
);