using FluentResults;
using System.Text;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Validators;

public class SidecarValidator : IFileValidator<SidecarDto>
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<Result<SidecarDto>> CheckAsync(string sidecarPath, CancellationToken ct)
    {

        if (!File.Exists(sidecarPath))
            return Result.Fail($"File not found: {Path.GetFileName(sidecarPath)}.");

        try
        {
            await using var fileStream = new FileStream(
                sidecarPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                8 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var streamReader = new StreamReader(
                fileStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: false);

            var sidecarJson = await streamReader.ReadToEndAsync(ct);

            var sidecar = JsonSerializer.Deserialize<SidecarDto>(sidecarJson, jsonSerializerOptions);

            if (sidecar is null)
                return Result.Fail($"Empty or invalid JSON in {Path.GetFileName(sidecarPath)}.");

            return sidecar!;
        }
        catch (JsonException ex)
        {
            return Result.Fail($"Error while parsing {Path.GetFileName(sidecarPath)}: {ex.Message}.");
        }
        catch (OperationCanceledException)
        {
            return Result.Fail($"Operation canceled.");
        }
    }
}
