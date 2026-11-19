namespace DidarCrm.Application.Abstractions.Messaging;

public interface IIntegrationEvent
{
    Guid MessageId { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
