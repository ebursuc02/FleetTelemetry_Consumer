using Telemetry.Application.Abstractions;

namespace Telemetry.Application.Utils;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
