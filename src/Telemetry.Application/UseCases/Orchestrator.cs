using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Telemetry.Application.Abstractions;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Entities;

namespace Telemetry.Application.UseCases;

public sealed class Orchestrator(IStore store, IConfiguration cfg) : BackgroundService
{
    private readonly TimeSpan Grace = TimeSpan.FromSeconds(1);
    private readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(10);

    private KpiRecord _kpi = new();
    private Sidecar _sidecar = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly string _folder = cfg["Orchestrator:KpiFolder"]!;

    // flush helper
    private DateTime _lastFlushUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(1000, ct); // delay to allow inboxConsumer enqueue records

            await RestoreSidecarAsync(ct);

            if (_sidecar.LastProcessedUtc == default)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                _sidecar.LastProcessedUtc = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
            }

            await CatchUpProccessDaysAsync(ct);
            await StoreSidecarAsync(ct);

            await DelayUntilNextMinuteAsync(Grace, ct);
            while (!ct.IsCancellationRequested)
            {
                var recs = store.GetAndClearRange(_sidecar.LastProcessedUtc, DateTime.UtcNow);
                AggregateKpi(recs);
                await MaybeFlushAsync(ct);
                await DelayUntilNextMinuteAsync(Grace, ct);
            }
        }
        catch (OperationCanceledException) when(ct.IsCancellationRequested)
        {
            // shutdown
        }
        finally
        {
            // fresh token to avoid instant cancel
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                if (_sidecar.LastProcessedUtc == default)
                    await RestoreSidecarAsync(shutdownCts.Token);
                var remaining = store.GetAndClearRange(_sidecar.LastProcessedUtc, DateTime.UtcNow);
                if (remaining.Any())
                {
                    AggregateKpi(remaining);
                    await StoreKpiAsync(shutdownCts.Token);
                    await StoreSidecarAsync(shutdownCts.Token);
                }
            }
            catch (OperationCanceledException) { /* timed out while shutting down */ }
        }
    }

    // --------------- catch-up processing (multiple day backup)  --------------- //

    private async Task CatchUpProccessDaysAsync(CancellationToken ct)
    {
        var startDay = DateOnly.FromDateTime(_sidecar.LastProcessedUtc);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var day = startDay; day <= today; day = day.AddDays(1))
        {
            _day = day;
            _kpi = new KpiRecord();

            await RestoreKpiAsync(day, ct);

            var endOfDay = today == day ? DateTime.UtcNow : EndOfDayUtc(day);
            var drained = store.GetAndClearRange(_sidecar.LastProcessedUtc, endOfDay);

            if (drained.Any())
            {
                _sidecar.LastProcessedUtc = 
                    day == today ? DateTime.UtcNow : day.ToDateTime(TimeOnly.MinValue).AddDays(1);
                continue;
            }

            AggregateKpi(drained);

            await StoreKpiAsync(ct);
            await StoreSidecarAsync(ct);
        }
    }

    // ------------------- aggregation ------------------- //

    private void AggregateKpi(IEnumerable<RecordDto> records)
    {
        double speedSum = _kpi.Speed * _kpi.SpeedRecords,
               oilSum = _kpi.OilTempC * _kpi.OilRecords,
               engineSum = _kpi.EngineRpm * _kpi.EngineRecords,
               co2Sum = _kpi.EmisionCO2 * _kpi.Co2Records;

        double maxFuel = double.MinValue, minFuel = double.MaxValue;
        int added = 0;

        foreach (var r in records)
        {
            added++;

            if (r.SpeedKmh is double s)
            {
                speedSum += s; _kpi.SpeedRecords++;
                if (s > 0) _kpi.IsMoving = true;
                else if (_kpi.IsMoving) { _kpi.StopsCount++; _kpi.IsMoving = false; }
            }
            if (r.OilTempC is double o) { oilSum += o; _kpi.OilRecords++; }
            if (r.EngineRpm is double e) { engineSum += e; _kpi.EngineRecords++; }
            if (r.EmisionCO2 is double c) { co2Sum += c; _kpi.Co2Records++; }

            if (r.FuelPct is double f)
            {
                if (f > maxFuel) maxFuel = f;
                if (f < minFuel) minFuel = f;
            }

            if (r.CoolantTempC is double ct && ct > 90) _kpi.CoolantHighTemperatureCount++;

            _sidecar.LastProcessedUtc = r.TsUtc > _sidecar.LastProcessedUtc ? r.TsUtc : _sidecar.LastProcessedUtc;
        }

        if (records.Any())
        {
            _kpi.RecordCount += added;
            if (_kpi.SpeedRecords > 0) _kpi.Speed = speedSum / _kpi.SpeedRecords;
            if (_kpi.OilRecords > 0) _kpi.OilTempC = oilSum / _kpi.OilRecords;
            if (_kpi.Co2Records > 0) _kpi.EmisionCO2 = co2Sum / _kpi.Co2Records;
            if (maxFuel >= minFuel) _kpi.FuelConsumption += (maxFuel - minFuel);
        }
        else _sidecar.LastProcessedUtc = DateTime.UtcNow;
    }

    // ------------------- persistence ------------------- //

    private async Task RestoreKpiAsync(DateOnly day, CancellationToken ct)
    {
        var path = FilePathFor(day);
        if (!File.Exists(path)) return;

        try
        {
            await using var fs = File.OpenRead(path);
            var persisted = await JsonSerializer.DeserializeAsync<KpiRecord>(fs, SerializerOptions, ct);
            if (persisted is null) return;

            _kpi = persisted;
        }
        catch { /* ignore */ }
    }

    private async Task StoreKpiAsync(CancellationToken ct)
    {
        var path = FilePathFor(_day);
        Directory.CreateDirectory(_folder);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, _kpi, SerializerOptions, ct);
    }

    private async Task RestoreSidecarAsync(CancellationToken ct)
    {
        var path = SidecarPath();
        if (!File.Exists(path)) { _sidecar = new Sidecar(); return; }

        try
        {
            await using var fs = File.OpenRead(path);
            _sidecar = (await JsonSerializer.DeserializeAsync<Sidecar>(fs, SerializerOptions, ct)) ?? new Sidecar();
        }
        catch { _sidecar = new Sidecar(); }
    }

    private async Task StoreSidecarAsync(CancellationToken ct)
    {
        var path = SidecarPath();
        Directory.CreateDirectory(_folder);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, _sidecar, SerializerOptions, ct);
        _lastFlushUtc = DateTime.UtcNow;
    }

    private async Task MaybeFlushAsync(CancellationToken ct)
    {
        var timeBased = (DateTime.UtcNow - _lastFlushUtc) >= FlushInterval;

        if (timeBased)
        {
            await StoreKpiAsync(ct);
            await StoreSidecarAsync(ct);
            _lastFlushUtc = DateTime.UtcNow;
        }
    }

    // ------------------- helper functions ------------------- //

    private static DateTime EndOfDayUtc(DateOnly day)
        => new DateTime(day.Year, day.Month, day.Day, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9990); // 23:59:59.9999999

    private string FilePathFor(DateOnly day) => Path.Combine(_folder, $"kpi_{day:yyyy-MM-dd}.json");
    private string SidecarPath() => Path.Combine(_folder, $"kpi.sidecar.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static async Task DelayUntilNextMinuteAsync(TimeSpan offset, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var next = TruncateToMinute(now).AddMinutes(1).Add(offset);
        var delay = next - now;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        await Task.Delay(delay, ct);
    }

    private static DateTime TruncateToMinute(DateTime dtUtc)
        => new(dtUtc.Year, dtUtc.Month, dtUtc.Day, dtUtc.Hour, dtUtc.Minute, 0, DateTimeKind.Utc);

}
