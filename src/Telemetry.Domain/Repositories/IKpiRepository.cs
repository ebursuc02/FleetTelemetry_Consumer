using FluentResults;
using Telemetry.Domain.Entities;

namespace Telemetry.Domain.Repositories;

public interface IKpiRepository
{
    Task<KpiRecord> LoadAsync(DateOnly day, CancellationToken ct);
    Task SaveAsync(DateOnly day, KpiRecord kpi, CancellationToken ct);
}
