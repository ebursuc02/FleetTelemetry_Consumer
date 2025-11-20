using FluentResults;
using System.Text;
using Telemetry.Application.DTOs;

namespace Telemetry.Application.Abstractions;

public interface IFileParser
{
    IAsyncEnumerable<Result<RecordDto>> ParseAsync(string filePath, CancellationToken ct = default);
}
