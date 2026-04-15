using SeismicFlow.Domain.Aggregates.Readings;

namespace SeismicFlow.Domain.Repositories;

public interface ISeismicReadingRepository
{
    Task AddAsync(SeismicReading reading, CancellationToken ct = default);

    Task<IReadOnlyList<SeismicReading>> GetByDeviceAsync(
        Guid deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? channel = null,
        int limit = 1000,
        CancellationToken ct = default);

    Task<SeismicReading?> GetLatestByDeviceAsync(
        Guid deviceId,
        string? channel = null,
        CancellationToken ct = default);
}