using FluentResults;

namespace Telemetry.Application.Abstractions;

public interface IIngestSource
{
    public IAsyncEnumerable<Result<string>> DiscoverAsync(CancellationToken ct = default);
}
