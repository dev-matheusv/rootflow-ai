using RootFlow.Application.Abstractions.Time;

namespace RootFlow.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
