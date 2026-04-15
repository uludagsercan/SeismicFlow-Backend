using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Shared.Results;

namespace SeismicFlow.Application.Common.Interfaces
{
    /// <summary>
    /// Abstracts PostgreSQL database provisioning for tenants.
    /// Implemented in Infrastructure.Persistence.
    /// </summary>
    public interface ITenantDatabaseProvisioner
    {
        Task<Result> ProvisionAsync(Tenant tenant, CancellationToken ct = default);
    }
}
