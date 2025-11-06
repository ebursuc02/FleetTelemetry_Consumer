using FluentResults;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileSidecarRepository(string sidecarFolder) : ISidecarRepository
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
        catch
        {
            return new Sidecar();
        }
    }


    public async Task<Result> SaveAsync(Sidecar state, CancellationToken ct)
    {
        var path = SidecarPath();
        Directory.CreateDirectory(sidecarFolder);

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, state, SerializerOptions, ct);
            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Sidecar saving couldn't be done.");
        }   
    }
}
