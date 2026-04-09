using SeismicFlow.Domain.Aggregates.Readings;

namespace SeismicFlow.Application.Common.Interfaces;

/// <summary>
/// Thin persistence abstraction used by the MQTT consumer background service.
/// Keeps Infrastructure.External decoupled from Infrastructure.Persistence.
/// </summary>
public interface ISeismicReadingPersister
{
    Task PersistAsync(Guid tenantId, SeismicReading reading, CancellationToken ct = default);
}