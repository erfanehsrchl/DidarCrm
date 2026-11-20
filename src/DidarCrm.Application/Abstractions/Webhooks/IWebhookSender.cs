namespace DidarCrm.Application.Abstractions.Webhooks;

public interface IWebhookSender
{
    Task SendAsync(
        Guid idempotencyKey,
        ActivityWebhookPayload payload,
        CancellationToken cancellationToken = default);
}
