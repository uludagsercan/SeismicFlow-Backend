using SeismicFlow.Domain.Common;

namespace SeismicFlow.Domain.Events
{
    /// <summary>
    /// Raised when a new Device is registered in the system.
    /// </summary>
    public sealed record DeviceCreatedEvent(
        Guid DeviceId,
        string DeviceSerialNumber,
        Guid TenantId) : DomainEvent;
}
