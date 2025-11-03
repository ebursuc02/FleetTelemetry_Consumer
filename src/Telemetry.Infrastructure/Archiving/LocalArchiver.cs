using System.Text.Json;
using Telemetry.Application.Abstractions;

namespace Telemetry.Infrastructure.Archiving;

public class LocalArchiver(string archive, string error) : IArchiver
{
    private readonly string _archive = archive; private readonly string _error = error;

    public Task ArchiveAsync(string data, string sidecar, string date, CancellationToken ct = default)
        => MovePair(data, sidecar, Path.Combine(_archive, date));

    public async Task ErrorAsync(string data, string sidecar, string reason, CancellationToken ct = default)
    {
        var dest = Path.Combine(_error, DateTime.UtcNow.ToString("yyyy/MM/dd"));
        await MovePair(data, sidecar, dest);
        await File.WriteAllTextAsync(Path.Combine(dest, Path.GetFileName(data) + ".error.json"),
            JsonSerializer.Serialize(new { reason, whenUtc = DateTime.UtcNow }), ct);
    }

    private static Task MovePair(string data, string sidecar, string dest)
    {
        Directory.CreateDirectory(dest);
        File.Move(data, Path.Combine(dest, Path.GetFileName(data)), true);
        File.Move(sidecar, Path.Combine(dest, Path.GetFileName(sidecar)), true);
        return Task.CompletedTask;
    }
}
