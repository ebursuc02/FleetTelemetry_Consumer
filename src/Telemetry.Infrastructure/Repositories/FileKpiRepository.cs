using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileKpiRepository(ILogger logger, string folderPath) : IKpiRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private string FilePathFor(DateOnly day) => Path.Combine(folderPath, $"kpi_{day:yyyy-MM-dd}.json");

    public async Task<KpiRecord> LoadAsync(DateOnly day, CancellationToken ct)
    {
        var path = FilePathFor(day);
        if (!File.Exists(path)) return new KpiRecord();

        try
        {
            await using var fs = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<KpiRecord>(fs, SerializerOptions, ct) ?? new KpiRecord();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentNullException)
        {
            logger.LogDebug("Parsing error occured, starting from a fresh object.");
            return new KpiRecord();
        }
    }

    public async Task SaveAsync(DateOnly day, KpiRecord kpi, CancellationToken ct)
    {
        

        var path = FilePathFor(day);
        Directory.CreateDirectory(folderPath);

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, kpi, SerializerOptions, ct);
            logger.LogDebug($"Kpi saved for day: {day}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentNullException)
        {
            logger.LogError($"Kpi couldn't be saved for day: {day}");
        }      
    }
}
