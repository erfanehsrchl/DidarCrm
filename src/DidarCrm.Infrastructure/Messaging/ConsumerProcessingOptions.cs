namespace DidarCrm.Infrastructure.Messaging;

public sealed class ConsumerProcessingOptions
{
    public const string SectionName = "ConsumerProcessing";

    public TimeSpan DuplicateDetectionWindow { get; init; } =
        TimeSpan.FromMinutes(30);

    public int RetryCount { get; init; } = 3;

    public TimeSpan RetryInterval { get; init; } =
        TimeSpan.FromSeconds(1);

    public TimeSpan[] WebhookRetryIntervals { get; init; } = [];
}
