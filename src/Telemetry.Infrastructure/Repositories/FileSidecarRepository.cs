using FluentResults;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileSidecarRepository(ILogger logger, string sidecarFolder) : ISidecarRepository
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
        if (!File.Exists(path)) return new Sidecar();

        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 32 * 1024, useAsync: true);
            return await JsonSerializer.DeserializeAsync<Sidecar>(fs, SerializerOptions, ct) ?? new Sidecar();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentNullException)
        {
            logger.LogDebug("Parsing error occured, starting from a fresh object.");
            return new Sidecar();
        }
    }


    public async Task SaveAsync(Sidecar state, CancellationToken ct)
    {
        var path = SidecarPath();
        Directory.CreateDirectory(sidecarFolder);

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, state, SerializerOptions, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentNullException)
        {

            logger.LogError("Sidecar saving couldn't be done.");
        }   
    }
}
