using FluentResults;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Validators;

public class FilePairValidator(IHasher hasher) : IFileValidator<SidecarDto>
{
    private const int BufferSize = 64 * 1024;
    private readonly IHasher _hasher = hasher;
    private readonly SidecarValidator _sidecarValidator = new();


    // TODO: refactoring
    public async Task<Result<SidecarDto>> CheckAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            return Result.Fail($"File not found: {Path.GetFileName(filePath)}.");

        var sidecarPath = filePath + ".meta.json";

        var sidecarCheckResult = await _sidecarValidator.CheckAsync(sidecarPath, ct);

        if (sidecarCheckResult.IsFailed)
            return sidecarCheckResult;

        var sidecarDto = sidecarCheckResult.Value!;

        if (sidecarDto.Sha256.Length == 0)
            return Result.Fail($"Sha not provided for: {Path.GetFileName(filePath)}.");

        var actualSha = _hasher.ComputeHex(filePath);
        if (!actualSha.Equals(sidecarDto.Sha256, StringComparison.OrdinalIgnoreCase))
            return Result.Fail($"Checksum mismatch for {Path.GetFileName(filePath)} (expected {sidecarDto.Sha256}, got {actualSha}).");

        var actualCount = await CountLinesAsync(filePath, ct);
        if (sidecarDto.RecordCount == 0 || sidecarDto.RecordCount != actualCount)
            return Result.Fail($"Record count mismatch for {Path.GetFileName(filePath)} (expected {sidecarDto.RecordCount}, got {actualCount}).");

        return sidecarDto;
    }

    private static async Task<long> CountLinesAsync(string path, CancellationToken ct)
    {
        await using var fileStream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buf = new byte[BufferSize];
        long lines = 0;
        int lastByte = -1;

        int n;
        while ((n = await fileStream.ReadAsync(buf, ct)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (buf[i] == (byte)'\n') lines++;
                lastByte = buf[i];
            }
        }

        if (fileStream.Length > 0 && lastByte != (byte)'\n') lines++;
        return lines;
    }
}
