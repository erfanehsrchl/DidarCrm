namespace DidarCrm.Infrastructure.Webhooks;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhook";

    public string Endpoint { get; init; } =
        "http://localhost:8080/webhooks/activities";
}
