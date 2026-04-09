using MediatR;
using SeismicFlow.Domain.Common;
using SeismicFlow.Domain.Repositories;
using SeismicFlow.Infrastructure.Persistence.Contexts;

namespace SeismicFlow.Infrastructure.Persistence
{

    /// <summary>
    /// Wraps SaveChangesAsync and dispatches domain events after commit.
    /// Domain events are collected by aggregate roots and dispatched here
    /// so they fire only after the transaction succeeds.
    /// </summary>
    public sealed class UnitOfWork(MasterDbContext masterDb, IPublisher publisher) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            // Collect domain events from all tracked aggregates before saving
            var domainEvents = masterDb.ChangeTracker
                .Entries<AggregateRoot>()
                .SelectMany(e => e.Entity.DomainEvents)
                .ToList();

            var result = await masterDb.SaveChangesAsync(ct);

            // Dispatch events after successful commit
            foreach (var domainEvent in domainEvents)
                await publisher.Publish(domainEvent, ct);

            // Clear events so they don't fire again
            masterDb.ChangeTracker
                .Entries<AggregateRoot>()
                .ToList()
                .ForEach(e => e.Entity.ClearDomainEvents());

            return result;
        }
    }
}
