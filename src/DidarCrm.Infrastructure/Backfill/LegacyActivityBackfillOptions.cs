namespace DidarCrm.Infrastructure.Backfill;

public sealed class LegacyActivityBackfillOptions
{
    public const string SectionName = "LegacyActivityBackfill";

    public bool Enabled { get; init; }

    public int BatchSize { get; init; } = 100;

    public TimeSpan BatchDelay { get; init; } =
        TimeSpan.FromMilliseconds(100);

    public TimeSpan PollingInterval { get; init; } =
        TimeSpan.FromSeconds(5);
}
