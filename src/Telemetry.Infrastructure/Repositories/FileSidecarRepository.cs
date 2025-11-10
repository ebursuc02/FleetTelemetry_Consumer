using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileSidecarRepository(ILogger<FileSidecarRepository> logger, string sidecarFolder) : ISidecarRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private string SidecarPath() => Path.Combine(sidecarFolder, $"kpi.sidecar.json");

    public async Task<Sidecar> LoadAsync(CancellationToken ct)
    {
        var path = SidecarPath();

        logger.LogDebug($"Loading sidecar from: {path}");

        if (!File.Exists(path))
        {
            logger.LogDebug($"No sidecar found at: {path}. Returning a new instance.");
            return new Sidecar();
        }

        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, useAsync: true);
            var sidecar = await JsonSerializer.DeserializeAsync<Sidecar>(fs, SerializerOptions, ct) ?? new Sidecar();
            logger.LogDebug($"Loaded sidecar from: {path}");
            return sidecar;
        }
        catch (OperationCanceledException) 
        {
            logger.LogDebug($"Load canceled for: {path}"); 
            throw; 
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentNullException)
        {
            logger.LogWarning(ex, $"Parsing error for sidecar at: {path}. Returning a fresh object.");
            return new Sidecar();
        }
    }


    public async Task SaveAsync(Sidecar state, CancellationToken ct)
    {
        var path = SidecarPath();
        Directory.CreateDirectory(sidecarFolder);
        logger.LogDebug($"Saving sidecar to: {path}");

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, state, SerializerOptions, ct);
            await fs.FlushAsync(ct);
            logger.LogDebug($"Saved sidecar to: {path}");
        }
        catch (OperationCanceledException) 
        {
            logger.LogDebug($"Save canceled for: {path}");
            throw;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentNullException)
        {
            logger.LogError(ex, $"Sidecar could not be saved to: {path}");
        }   
    }
}
