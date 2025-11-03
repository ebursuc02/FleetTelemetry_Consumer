namespace Telemetry.Application.DTOs;

public class SidecarDto
{
    public required string Version { get; init; }
    public DateTime CreatedUtc { get; init; }
    public required int RecordCount { get; init; }
    public required string Sha256 { get; init; }
    public required string Encoding { get; init; }
}
