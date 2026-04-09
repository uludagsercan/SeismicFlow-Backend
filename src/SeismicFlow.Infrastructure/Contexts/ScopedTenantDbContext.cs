using Microsoft.AspNetCore.Http;

namespace SeismicFlow.Infrastructure.Persistence.Contexts;

/// <summary>
/// Scoped service that lazily creates a TenantDbContext on first access
/// and caches it for the lifetime of the DI scope.
/// 
/// Both DeviceRepository and TenantUnitOfWork depend on this,
/// guaranteeing they operate on the same DbContext instance.
/// </summary>
public sealed class ScopedTenantDbContext(
    ITenantDbContextFactory factory,
    IHttpContextAccessor httpContextAccessor) : IScopedTenantDbContext, IAsyncDisposable
{
    private TenantDbContext? _context;

    public async Task<TenantDbContext> GetAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            return _context;

        var tenantId = ResolveTenantId();
        _context = await factory.CreateAsync(tenantId, ct);
        return _context;
    }

    private Guid ResolveTenantId()
    {
        if (httpContextAccessor.HttpContext?.Items["TenantId"] is Guid tenantId)
            return tenantId;

        throw new InvalidOperationException(
            "TenantId not found in HttpContext. Ensure TenantMiddleware is registered.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
            await _context.DisposeAsync();
    }
}