using System.Text;
using System.Text.Json;
using Telemetry.Application.Results;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Parsing;

public sealed class JsonlsParser : IFileParser
{
    private const int BufferSize = 64 * 1024;

    public async IAsyncEnumerable<Result<RecordDto>> ParseAsync(
        string filePath,
        Encoding encoding,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var openResult = await TryOpenForReadAsync(filePath, ct: ct);
        if (!openResult.Success)
        {
            yield return Result<RecordDto>.Fail(openResult.Error!);
            yield break;
        }

        await using var fileStream = openResult.Value!;

        using var streamReader = new StreamReader(
            fileStream,
            encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: BufferSize,
            leaveOpen: false);

        string? line;

        while (!ct.IsCancellationRequested && (line = await streamReader.ReadLineAsync()) is not null)
        {
            if (line.Length == 0) continue;

            var dto = JsonSerializer.Deserialize<RecordDto>(line, jsonOptions);
            if (dto is not null) yield return Result<RecordDto>.Ok(dto);
        }
    }

    private static async Task<Result<FileStream>> TryOpenForReadAsync(
        string path,
        int maxAttempts = 3,
        int initialDelayMs = 100,
        CancellationToken ct = default)
    {

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
                return Result<FileStream>.Fail("Operation was canceled.");

            try
            {
                var fs = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                return Result<FileStream>.Ok(fs);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                // retry
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // retry
            }
            catch (Exception)
            {
                // unknown error
                return Result<FileStream>.Fail("Unexpected error while opening the file.");
            }

            try
            {
                var delay = TimeSpan.FromMilliseconds(Math.Min(initialDelayMs * (1 << (attempt - 1)), 1000));
                await Task.Delay(delay, ct);
            }
            catch (TaskCanceledException)
            {
                return Result<FileStream>.Fail("Operation was canceled.");
            }
        }

        return Result<FileStream>.Fail($"Could not open file '{path}' after {maxAttempts} attempts.");
    }
}