using AutoMapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Abstractions;
using Telemetry.Domain.Entities;
using Telemetry.Domain.Repositories;

namespace Telemetry.Application.UseCases;

public sealed class KPIOrchestratorHandler(
    BlockingCollection<RecordDto> storage,
    IKpiRepository kpiRepo,
    ISidecarRepository sidecarRepo,
    IStopAccumulator stops,
    IFuelAccumulator fuel,
    IFlushPolicy flush,
    IClock clock,
    IMapper mapper,
    ILogger logger) : BackgroundService
{
    private KpiRecord _kpi = new();
    private Sidecar _sidecar = new();
    private DateOnly _day;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RestoreAsync(ct);

        try
        {
            foreach (var r in storage.GetConsumingEnumerable(ct))
            {
                var recordDay = DateOnly.FromDateTime(r.TsUtc);
                if (recordDay != _day)
                {
                    await PersistAsync(ct);
                    _day = recordDay;
                    _kpi = await kpiRepo.LoadAsync(_day, ct);
                    fuel.ResetForNewDay();
                }

                _kpi.Apply(mapper.Map<Record>(r), stops, fuel);

                _sidecar.LastProcessedUtc = r.TsUtc;
                _sidecar.LastProcessedVeh = r.VehicleId;

                if (flush.ShouldFlush(clock.UtcNow))
                    await PersistAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Operation canceled, processing last records...");
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await PersistAsync(shutdown.Token); } 
            catch { logger.LogDebug("Timeout, persistence aborted."); }
        }
    }

    private async Task RestoreAsync(CancellationToken ct)
    {
        _sidecar = await sidecarRepo.LoadAsync(ct);

        if (string.IsNullOrEmpty(_sidecar.LastProcessedVeh))
            _day = DateOnly.FromDateTime(DateTime.UtcNow);
        else
        {
            // pass all duplicates
            foreach (var rec in storage.GetConsumingEnumerable(ct))
            {
                if (rec.VehicleId == _sidecar.LastProcessedVeh
                    && rec.TsUtc <= _sidecar.LastProcessedUtc) // unique identifiers for records
                    continue;

                break;
            }   
            _day = DateOnly.FromDateTime(_sidecar.LastProcessedUtc);
        }
  
        _kpi = await kpiRepo.LoadAsync(_day, ct);
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        await kpiRepo.SaveAsync(_day, _kpi, ct);
        await sidecarRepo.SaveAsync(_sidecar, ct);
        flush.MarkFlushed(clock.UtcNow);
    }
}
