namespace DidarCrm.Infrastructure.Messaging;

public sealed class OutboxDeliveryOptions
{
    public const string SectionName = "OutboxDelivery";

    public bool Enabled { get; init; }

    public int QueryMessageLimit { get; init; } = 100;

    public int MessageDeliveryLimit { get; init; } = 100;

    public TimeSpan QueryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan MessageDeliveryTimeout { get; init; } =
        TimeSpan.FromSeconds(30);
}
