using Microsoft.EntityFrameworkCore;
using SeismicFlow.Domain.Aggregates.Tenants;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Domain.ValueObjects;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence.Repositories
{
    public sealed class TenantRepository(MasterDbContext db) : ITenantRepository
    {
        public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

        public async Task<Tenant?> GetBySlugAsync(TenantSlug slug, CancellationToken ct = default) =>
            await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

        public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) =>
            await db.Tenants.ToListAsync(ct);

        public async Task<bool> ExistsBySlugAsync(TenantSlug slug, CancellationToken ct = default) =>
            await db.Tenants.AnyAsync(t => t.Slug == slug, ct);

        public void Add(Tenant tenant) => db.Tenants.Add(tenant);

        public void Update(Tenant tenant) => db.Tenants.Update(tenant);
    }
}
