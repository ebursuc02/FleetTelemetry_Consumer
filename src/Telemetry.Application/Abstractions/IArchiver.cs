namespace Telemetry.Application.Abstractions;

public interface IArchiver
{
    public Task ArchiveAsync(string dataPath, string sidecarPath, string date, CancellationToken ct = default);
    public Task ErrorAsync(string dataPath, string sidecarPath, string reason, CancellationToken ct = default);
}
