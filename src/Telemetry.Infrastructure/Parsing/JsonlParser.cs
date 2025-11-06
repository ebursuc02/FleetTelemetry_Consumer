using FluentResults;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Parsing;

public sealed class JsonlsParser(IConfiguration cfg) : IFileParser
{
    private readonly int _bufferSize = int.Parse(cfg["Parsing:BufferSize"]!);
    private readonly int _maxFileOpenningAttempts = int.Parse(cfg["Parsing:MaxAttempts"]!);

    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };


    public async IAsyncEnumerable<Result<RecordDto>> ParseAsync(
    string filePath,
    [EnumeratorCancellation] CancellationToken ct = default)
    {
        var open = await OpenReadWithRetryAsync(filePath, ct: ct);
        if (open.IsFailed)
        {
            yield return Result.Fail(open.Errors);
            yield break;
        }

        await using var stream = open.Value;

        await foreach (var res in ParseStreamAsync(stream, ct))
            yield return res;

        if (ct.IsCancellationRequested)
            yield return Result.Fail("Operation was canceled.");
    }

    private async IAsyncEnumerable<Result<RecordDto>> ParseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var (line, lineNo) in ReadNonEmptyLines(stream, ct))
            yield return DeserializeRecord(line, lineNo);
    }

    private async IAsyncEnumerable<(string line, int lineNo)> ReadNonEmptyLines(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: _bufferSize, leaveOpen: true);

        string? line;
        var lineNo = 0;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNo++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            yield return (line, lineNo);
        }
    }

    private static Result<RecordDto> DeserializeRecord(string line, int lineNo)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<RecordDto>(line, JsonOptions);
            return dto is null
                ? Result.Fail($"Null JSON object at line {lineNo}. Line: {Trunc(line)}")
                : Result.Ok(dto);
        }
        catch (JsonException jx)
        {
            return Result.Fail($"JSON parse error at line {lineNo}: {jx.Message}. Near: {Trunc(line)}");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Unexpected error at line {lineNo}: {ex.Message}. Near: {Trunc(line)}");
        }
    }

    private async Task<Result<FileStream>> OpenReadWithRetryAsync(
        string path,
        CancellationToken ct = default)
    {
        Exception? lastRetryable = null;

        for (var attempt = 1; attempt <= _maxFileOpenningAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var fs = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    _bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                return fs;
            }
            catch (FileNotFoundException e) { return Result.Fail($"File not found: '{path}'. {e.Message}"); }
            catch (DirectoryNotFoundException e) { return Result.Fail($"Directory not found for: '{path}'. {e.Message}"); }
            catch (PathTooLongException e) { return Result.Fail($"Path too long: '{path}'. {e.Message}"); }
            catch (UnauthorizedAccessException e) when (attempt < _maxFileOpenningAttempts) { lastRetryable = e; }
            catch (IOException e) when (attempt < _maxFileOpenningAttempts) { lastRetryable = e; }
            catch (Exception e) { return Result.Fail($"Unexpected error opening '{path}': {e.Message}"); }

            try { await Task.Delay(1000, ct); }
            catch (TaskCanceledException) { return Result.Fail("Operation was canceled."); }
        }

        var suffix = lastRetryable is null ? "" : $" Last error: {lastRetryable.Message}";
        return Result.Fail($"Could not open file '{path}' after {_maxFileOpenningAttempts} attempts.{suffix}");
    }

    private static string Trunc(string s, int max = 160) => s.Length <= max ? s : s[..max] + "…";
}
