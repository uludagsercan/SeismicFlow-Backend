using MediatR;
using SeismicFlow.Domain.Common;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence;

/// <summary>
/// Unit of Work for tenant-scoped operations.
/// Wraps the scoped TenantDbContext, saves changes, and dispatches domain events.
/// </summary>
public sealed class TenantUnitOfWork(
    IScopedTenantDbContext scopedContext,
    IPublisher publisher) : ITenantUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var db = await scopedContext.GetAsync(ct);

        // Collect domain events from all tracked aggregates before saving
        var domainEvents = db.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await db.SaveChangesAsync(ct);

        // Dispatch events after successful commit
        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, ct);

        // Clear events so they don't fire again
        db.ChangeTracker
            .Entries<AggregateRoot>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        return result;
    }
}