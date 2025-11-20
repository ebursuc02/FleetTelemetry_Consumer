using Telemetry.Application.DTOs;

namespace Telemetry.Application.Abstractions;

public interface IProcessingStatusWriter
{
    Task MarkProcessedAsync(SidecarDto sidecar, string sidecarPath, CancellationToken ct = default);
    Task WriteErrorAsync(SidecarDto sidecar, string sidecarPath, string reason, CancellationToken ct = default);
}
