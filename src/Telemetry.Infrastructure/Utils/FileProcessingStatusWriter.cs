using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;

namespace Telemetry.Infrastructure.Utils;

public class FileProcessingStatusWriter(ILogger logger) : IProcessingStatusWriter
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
        logger.LogDebug("\n---- Error file ----");
        logger.LogDebug($"Reason: {reason}");
        await WriteJsonAtomicAsync(sidecarPath, sidecar, ct);
    }

    private async Task WriteJsonAtomicAsync(string path, object obj, CancellationToken ct)
    {
        try
        {
            await Task.Delay(1000, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 64 * 1024, useAsync: true);
            await JsonSerializer.SerializeAsync(fs, obj, JsonOpts, ct);
            await fs.FlushAsync(ct);
            logger.LogDebug($"Proccessed: {path}");
        } catch (IOException)
        {
            logger.LogError($"File {Path.GetFileName(path)} is accessed by another process.");
        }
        
    }

}
