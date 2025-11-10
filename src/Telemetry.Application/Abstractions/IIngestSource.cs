using FluentResults;

namespace Telemetry.Application.Abstractions;

public interface IIngestSource
{
    public IAsyncEnumerable<string> DiscoverAsync(CancellationToken ct = default);
}
