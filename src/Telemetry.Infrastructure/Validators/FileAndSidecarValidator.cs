using FluentResults;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Validators;

public sealed class FileAndSidecarValidator(IConfiguration cfg) : IFileValidator<SidecarDto>
{
    private readonly int BufferSize = int.Parse(cfg["Parsing:BufferSize"]!);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<SidecarDto>> CheckAsync(string dataPath, CancellationToken ct)
    {
        var dataName = Path.GetFileName(dataPath);
        var sidecarPath = dataPath + ".meta.json";

        foreach (var (path, label) in new[] { (dataPath, "File"), (sidecarPath, "Sidecar") })
            if (!File.Exists(path))
                return Result.Fail($"{label} not found: {Path.GetFileName(path)}.");

        SidecarDto? sc;
        try
        {
            await using var fs = new FileStream(
                sidecarPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                8 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            sc = await JsonSerializer.DeserializeAsync<SidecarDto>(fs, JsonOpts, ct);
        }
        catch (JsonException ex) { return Result.Fail($"Error parsing {Path.GetFileName(sidecarPath)}: {ex.Message}."); }
        catch (OperationCanceledException) { return Result.Fail("Operation canceled."); }
        catch (IOException ex) { return Result.Fail($"I/O error reading {Path.GetFileName(sidecarPath)}: {ex.Message}."); }

        if (sc is null)
            return Result.Fail($"Empty or invalid JSON in {Path.GetFileName(sidecarPath)}.");

        if (string.IsNullOrWhiteSpace(sc.Sha256))
            return Result.Fail($"SHA-256 not provided for {dataName}.");

        var (actualSha, actualCount) = await HashAndCountAsync(dataPath, ct);

        if (!actualSha.Equals(sc.Sha256, StringComparison.OrdinalIgnoreCase))
            return Result.Fail($"Checksum mismatch for {dataName} (expected {sc.Sha256}, got {actualSha}).");

        if (sc.RecordCount <= 0 || actualCount != sc.RecordCount)
            return Result.Fail($"Record count mismatch for {dataName}: {sc.RecordCount}.");

        return sc;
    }

    private async Task<(string Sha256Hex, long LineCount)> HashAndCountAsync(
    string path, CancellationToken ct)
    {
        await using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var inc = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        byte[] buffer = new byte[128 * 1024];
        long lines = 0;
        int last = -1;

        int n;
        while ((n = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            inc.AppendData(buffer, 0, n);

            for (int i = 0; i < n; i++)
                if (buffer[i] == (byte)'\n') lines++;

            last = buffer[n - 1];
        }

        if (fs.Length > 0 && last != (byte)'\n') lines++;

        var hash = inc.GetHashAndReset();
        var hex = Convert.ToHexString(hash);
        return (hex, lines);
    }

}
