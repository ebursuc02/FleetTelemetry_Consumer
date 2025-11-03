using System.Text;
using Telemetry.Application.DTOs;
using Telemetry.Application.Results;

namespace Telemetry.Application.Abstractions;

public interface IFileParser
{
    IAsyncEnumerable<Result<RecordDto>> ParseAsync(string filePath, Encoding encoding, CancellationToken ct = default);
}
