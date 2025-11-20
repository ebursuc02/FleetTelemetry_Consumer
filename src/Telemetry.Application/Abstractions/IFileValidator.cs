using FluentResults;


namespace Telemetry.Application.Abstractions;

public interface IFileValidator<T>
{
    public Task<Result<T>> CheckAsync(string filePath, CancellationToken ct);
}
