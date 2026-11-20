namespace DidarCrm.Application.Abstractions.Webhooks;

public interface IWebhookSender
{
    Task SendAsync(
        ActivityWebhookPayload payload,
        CancellationToken cancellationToken = default);
}
