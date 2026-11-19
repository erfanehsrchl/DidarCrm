using DidarCrm.Application.Abstractions.Messaging;

namespace DidarCrm.Application.Activities.Events;

public sealed record ActivityCreatedEvent(
    Guid MessageId,
    Guid ActivityId,
    Guid TenantId,
    Guid ContactId,
    string Title,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent;
