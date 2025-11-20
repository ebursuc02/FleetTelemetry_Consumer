using System.Globalization;

namespace Telemetry.Application.DTOs;

public class SidecarDto
{
    public string? Version { get; init; }
    public DateTime CreatedUtc { get; init; }
    public int? RecordCount { get; init; }
    public string? Sha256 { get; init; }
    public string? Encoding { get; init; }
    public bool Processed { get; set; } = false;
    public string? ErrorReason { get; set; }
    public string? ProducerName { get; init; }
}
