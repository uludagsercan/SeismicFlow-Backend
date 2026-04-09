using SeismicFlow.Domain.Common;

namespace SeismicFlow.Domain.Events
{
    /// <summary>
    /// Raised when a new Tenant record is created (status = Provisioning).
    /// </summary>
    public sealed record TenantCreatedEvent(
        Guid TenantId,
        string Slug) : DomainEvent;

    /// <summary>
    /// Raised when a Tenant transitions from Provisioning to Active.
    /// </summary>
    public sealed record TenantActivatedEvent(
        Guid TenantId,
        string Slug) : DomainEvent;
}
