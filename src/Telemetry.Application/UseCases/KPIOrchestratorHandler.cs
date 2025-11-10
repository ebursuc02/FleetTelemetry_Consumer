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
        logger.LogInformation("KPIOrchestrator started");
        await RestoreAsync(ct);

        int processedSinceLastPersist = 0;
        int totalProcessed = 0;
        int daySwitches = 0;

        try
        {
            foreach (var r in storage.GetConsumingEnumerable(ct))
            {
                if (!string.IsNullOrEmpty(_sidecar.LastProcessedVeh) &&
                    r.VehicleId == _sidecar.LastProcessedVeh &&
                    r.TsUtc <= _sidecar.LastProcessedUtc)
                {
                    logger.LogDebug($"Skipping duplicate record {r.VehicleId} @ {r.TsUtc:o}");
                    continue;
                }

                var recordDay = DateOnly.FromDateTime(r.TsUtc);
                if (recordDay != _day)
                {
                    logger.LogInformation($"Day change: {_day:yyyy-MM-dd} -> {recordDay:yyyy-MM-dd}. Persisting current KPI...");
                    await PersistAsync(ct);
                    _day = recordDay;
                    _kpi = await kpiRepo.LoadAsync(_day, ct);
                    fuel.ResetForNewDay();
                    processedSinceLastPersist = 0;
                    daySwitches++;
                }

                // apply record to kpi
                _kpi.Apply(mapper.Map<Record>(r), stops, fuel);
                _sidecar.LastProcessedUtc = r.TsUtc;
                _sidecar.LastProcessedVeh = r.VehicleId;

                processedSinceLastPersist++;
                totalProcessed++;

                // flush if needed
                if (flush.ShouldFlush(clock.UtcNow))
                {
                    logger.LogDebug($"Flush policy triggered. Persisting KPI for {_day:yyyy-MM-dd} after {processedSinceLastPersist} records...");
                    await PersistAsync(ct);
                    processedSinceLastPersist = 0;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("KPIOrchestrator stopping (cancellation requested). Persisting last state...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KPIOrchestrator crashed with an unhandled exception");
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await PersistAsync(shutdown.Token);
                logger.LogInformation("Final persistence completed during shutdown window.");
            }
            catch
            {
                logger.LogDebug("Shutdown persistence aborted (timeout or cancellation).");
            }

            logger.LogInformation($"KPIOrchestrator stopped. Total records applied: {totalProcessed}, day switches: {daySwitches}");
        }
    }

    private async Task RestoreAsync(CancellationToken ct)
    {
        logger.LogDebug("Restoring sidecar/KPI state...");
        _sidecar = await sidecarRepo.LoadAsync(ct);

        if (string.IsNullOrEmpty(_sidecar.LastProcessedVeh))
        {
            _day = DateOnly.FromDateTime(DateTime.UtcNow);
            logger.LogDebug($"No previous state found. Starting fresh at day {_day:yyyy-MM-dd}.");
        }
        else
        {
            _day = DateOnly.FromDateTime(_sidecar.LastProcessedUtc);
            logger.LogDebug($"Restored last processed: veh={_sidecar.LastProcessedVeh}, ts={_sidecar.LastProcessedUtc:o}, day={_day:yyyy-MM-dd}.");
        }

        _kpi = await kpiRepo.LoadAsync(_day, ct);
        logger.LogDebug($"Loaded KPI snapshot for {_day:yyyy-MM-dd}.");
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        try
        {
            logger.LogDebug($"Persisting KPI({_day:yyyy-MM-dd}) and sidecar...");
            await kpiRepo.SaveAsync(_day, _kpi, ct);
            await sidecarRepo.SaveAsync(_sidecar, ct);
            flush.MarkFlushed(clock.UtcNow);
            logger.LogDebug("Persistence complete.");
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Persistence canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Persistence failed.");
            throw;
        }
    }
}
