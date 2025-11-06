using FluentResults;
using System.Text.Json;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Infrastructure.Repositories;

public class FileKpiRepository(string folderPath) : IKpiRepository
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
        catch { return new KpiRecord(); }
    }

    public async Task<Result> SaveAsync(DateOnly day, KpiRecord kpi, CancellationToken ct)
    {
        Console.WriteLine($"Kpi saved for day: {day}");

        var path = FilePathFor(day);
        Directory.CreateDirectory(folderPath);

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, kpi, SerializerOptions, ct);
            return Result.Ok();
        } 
        catch
        {
            return Result.Fail("Kpi saving couldn't be done");
        }      
    }
}
