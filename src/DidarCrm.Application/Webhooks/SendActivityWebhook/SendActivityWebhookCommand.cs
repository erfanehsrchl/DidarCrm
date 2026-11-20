namespace DidarCrm.Application.Webhooks.SendActivityWebhook;

public sealed record SendActivityWebhookCommand(
    Guid ActivityId,
    Guid TenantId,
    Guid ContactId,
    string Title,
    DateTimeOffset ActivityCreatedAtUtc);
