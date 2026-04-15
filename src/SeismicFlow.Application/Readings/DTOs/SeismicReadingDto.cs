namespace SeismicFlow.Application.Readings.DTOs;

public sealed record SeismicReadingDto(
    Guid Id,
    Guid DeviceId,
    string Channel,
    DateTimeOffset Timestamp,
    double SampleRate,
    int SampleCount,
    double[] Samples
);
