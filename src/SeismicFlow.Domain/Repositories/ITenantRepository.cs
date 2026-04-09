using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Domain.ValueObjects;

namespace SeismicFlow.Domain.Repositories
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Tenant?> GetBySlugAsync(TenantSlug slug, CancellationToken ct = default);
        Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
        Task<bool> ExistsBySlugAsync(TenantSlug slug, CancellationToken ct = default);
        void Add(Tenant tenant);
        void Update(Tenant tenant);
    }
}
