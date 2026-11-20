using System.Net.Http.Json;
using DidarCrm.Application.Abstractions.Webhooks;

namespace DidarCrm.Infrastructure.Webhooks;

public sealed class HttpWebhookSender(
    HttpClient httpClient,
    WebhookOptions options) : IWebhookSender
{
    public async Task SendAsync(
        Guid idempotencyKey,
        ActivityWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            options.Endpoint);
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
