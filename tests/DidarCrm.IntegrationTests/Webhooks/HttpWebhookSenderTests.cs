using System.Net;
using System.Net.Http.Json;
using DidarCrm.Application.Abstractions.Webhooks;
using DidarCrm.Infrastructure.Webhooks;

namespace DidarCrm.IntegrationTests.Webhooks;

public sealed class HttpWebhookSenderTests
{
    [Fact]
    public async Task Send_posts_payload_with_activity_id_as_idempotency_key()
    {
        var messageHandler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(messageHandler);
        var options = new WebhookOptions
        {
            Endpoint = "https://webhook.example.test/activities"
        };
        var sender = new HttpWebhookSender(httpClient, options);
        var payload = new ActivityWebhookPayload(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Follow up",
            new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

        await sender.SendAsync(payload.ActivityId, payload);

        Assert.Equal(HttpMethod.Post, messageHandler.Method);
        Assert.Equal(options.Endpoint, messageHandler.RequestUri?.ToString());
        Assert.Equal(
            payload.ActivityId.ToString(),
            messageHandler.IdempotencyKey);
        Assert.Equal(payload, messageHandler.Payload);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public ActivityWebhookPayload? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            IdempotencyKey = request.Headers
                .GetValues("Idempotency-Key")
                .Single();
            Payload = await request.Content!
                .ReadFromJsonAsync<ActivityWebhookPayload>(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
