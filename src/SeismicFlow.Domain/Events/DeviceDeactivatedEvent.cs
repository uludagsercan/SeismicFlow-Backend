using SeismicFlow.Domain.Common;

namespace SeismicFlow.Domain.Events
{
    /// <summary>
    /// Raised when a Device is deactivated.
    /// Device data is retained but no new waveform packets will be processed.
    /// </summary>
    public sealed record DeviceDeactivatedEvent(
        Guid DeviceId,
        string DeviceSerialNumber) : DomainEvent;
}
