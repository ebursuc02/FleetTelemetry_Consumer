using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileKpiRepository(ILogger<FileKpiRepository> logger, string folderPath) : IKpiRepository
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
        logger.LogDebug($"Loading KPI for day {day} from: {path}");

        if (!File.Exists(path))
        {
            logger.LogDebug($"No KPI file found at: {path}. Returning a new record.");
            return new KpiRecord();
        }

        try
        {
            await using var fs = File.OpenRead(path);
            var rec = await JsonSerializer.DeserializeAsync<KpiRecord>(fs, SerializerOptions, ct) ?? new KpiRecord();
            logger.LogDebug($"Loaded KPI for day {day} from: {path}");
            return rec;
        }
        catch (OperationCanceledException) 
        {
            logger.LogDebug($"Load canceled for day {day} from: {path}"); 
            throw; 
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentNullException)
        {
            logger.LogWarning(ex, $"Parsing error for KPI file at: {path}. Returning a fresh record.");
            return new KpiRecord();
        }
    }

    public async Task SaveAsync(DateOnly day, KpiRecord kpi, CancellationToken ct)
    {
        

        var path = FilePathFor(day);
        Directory.CreateDirectory(folderPath);
        logger.LogDebug($"Saving KPI for day {day} to: {path}");

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, kpi, SerializerOptions, ct);
            await fs.FlushAsync(ct);
            logger.LogDebug($"KPI saved for day {day} to: {path}");
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug($"Save canceled for day {day} to: {path}");
            throw;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentNullException)
        {
            logger.LogError(ex, $"KPI could not be saved for day {day} to: {path}");
        }
    }
}
