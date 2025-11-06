using Telemetry.Domain.Abstractions;

namespace Telemetry.Domain.Entities;

public class KpiRecord
{
    public long RecordCount { get; set; } = 0;
    public double Speed { get; set; } = 0;
    public long SpeedRecords { get; set; } = 0;
    public double OilTempC { get; set; } = 0;
    public long OilRecords { get; set; } = 0;
    public double EngineRpm { get; set; } = 0;
    public long EngineRecords { get; set; } = 0;
    public double EmisionCO2 { get; set; } = 0;
    public long Co2Records { get; set; } = 0;
    public long CoolantHighTemperatureCount { get; set; } = 0;
    public double FuelConsumption { get; set; } = 0;
    public double StopsCount { get; set; } = 0;


    public void Apply(Record record, IStopAccumulator stopAccumulator, IFuelAccumulator fuelAccumulator)
    {
        RecordCount++;

        if (record.SpeedKmh is double speed)
        {
            if (speed > 0) { Speed = Avg(Speed, SpeedRecords, speed); SpeedRecords++; }
            stopAccumulator.Observe(record.VehicleId, speed);
            StopsCount = stopAccumulator.CurrentAverage();
        }

        if (record.OilTempC is double oil) { OilTempC = Avg(OilTempC, OilRecords, oil); OilRecords++; }
        if (record.EngineRpm is double engine) { EngineRpm = Avg(EngineRpm, EngineRecords, engine); EngineRecords++; }
        if (record.Co2 is double co2) { EmisionCO2 = Avg(EmisionCO2, Co2Records, co2); Co2Records++; }

        if (record.CoolantTempC is double ct && ct > 90) CoolantHighTemperatureCount++;

        if (record.FuelPct is double fuel)
        {
            fuelAccumulator.Observe(record.VehicleId, fuel);
            FuelConsumption = fuelAccumulator.CurrentAverage();
        }

    }

    private static double Avg(double currentAvg, long objectCount, double newRec)
        => Math.Round(((currentAvg * objectCount) + newRec) / (objectCount + 1), 4, MidpointRounding.AwayFromZero);
}
