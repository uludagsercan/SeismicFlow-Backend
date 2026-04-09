using MediatR;

namespace SeismicFlow.Domain.Common
{
    public interface IDomainEvent: INotification
    {
        Guid EventId { get; }
        DateTimeOffset OccurredAt { get; }
    }
}
