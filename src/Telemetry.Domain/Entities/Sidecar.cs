namespace Telemetry.Domain.Entities;

public class Sidecar
{
    public string LastProcessedVeh { get; set; } = string.Empty;
    public DateTime LastProcessedUtc { get; set; }
}
