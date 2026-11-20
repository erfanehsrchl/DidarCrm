namespace DidarCrm.Application.Abstractions.Webhooks;

public sealed record ActivityWebhookPayload(
    Guid ActivityId,
    Guid TenantId,
    Guid ContactId,
    string Title,
    DateTimeOffset CreatedAtUtc);
