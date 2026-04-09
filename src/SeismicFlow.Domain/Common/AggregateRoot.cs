namespace SeismicFlow.Domain.Common
{
    /// <summary>
    /// Base class for all aggregate roots.
    /// Provides domain event collection so the infrastructure layer
    /// can dispatch them after the transaction commits.
    /// </summary>
    public abstract class AggregateRoot
    {
        public Guid Id { get; protected set; }

        private readonly List<IDomainEvent> _domainEvents = [];

        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
            _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
