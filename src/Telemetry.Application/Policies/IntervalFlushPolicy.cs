using Telemetry.Application.Abstractions;

namespace Telemetry.Application.Policies;

public class IntervalFlushPolicy(TimeSpan interval) : IFlushPolicy
{
    private DateTime _last = DateTime.MinValue;
    public bool ShouldFlush(DateTime now) => (now - _last) >= interval;
    public void MarkFlushed(DateTime now) => _last = now;
}
