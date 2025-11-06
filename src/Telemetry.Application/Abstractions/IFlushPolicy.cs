namespace Telemetry.Application.Abstractions;

public interface IFlushPolicy
{
    bool ShouldFlush(DateTime utcNow);
    void MarkFlushed(DateTime utcNow);
}
