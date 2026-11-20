namespace DidarCrm.Infrastructure.Messaging;

public static class MessagingTopology
{
    public const string ActivityCreatedExchange =
        "didar-crm.activity-created";

    public const string NotificationQueue =
        "didar-crm.notification.activity-created";

    public const string WebhookQueue =
        "didar-crm.webhook.activity-created";

}
