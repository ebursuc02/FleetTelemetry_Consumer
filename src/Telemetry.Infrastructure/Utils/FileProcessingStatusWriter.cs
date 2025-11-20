using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Utils;

public class FileProcessingStatusWriter(ILogger<FileProcessingStatusWriter> logger) : IProcessingStatusWriter
{

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        // WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task MarkProcessedAsync(SidecarDto sidecar, string sidecarPath, CancellationToken ct)
    {
        sidecar.Processed = true;
        await WriteJsonAtomicAsync(sidecarPath, sidecar, ct);
    }

    public async Task WriteErrorAsync(SidecarDto sidecar, string sidecarPath, string reason, CancellationToken ct)
    {
        sidecar.Processed = true;
        sidecar.ErrorReason = reason;
        logger.LogDebug($"Error file. Reason: {reason}");
        await WriteJsonAtomicAsync(sidecarPath, sidecar, ct);
    }

    private async Task WriteJsonAtomicAsync(string path, object obj, CancellationToken ct)
    {
        try
        {
            await Task.Delay(2000, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            logger.LogDebug($"Writing JSON to: {path}");
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 64 * 1024, useAsync: true);
            await JsonSerializer.SerializeAsync(fs, obj, JsonOpts, ct);
            await fs.FlushAsync(ct);

            logger.LogDebug($"Processed: {path}");
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug($"Write canceled for: {path}");
            throw;
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, $"File {Path.GetFileName(path)} is accessed by another process.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to write JSON to: {path}");
            throw;
        }
    }

}
