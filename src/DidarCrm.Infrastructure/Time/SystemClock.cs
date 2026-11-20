using DidarCrm.Application.Abstractions.Time;

namespace DidarCrm.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
