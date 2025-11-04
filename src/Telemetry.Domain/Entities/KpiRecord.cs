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
    public bool IsMoving { get; set; } = true;
    public long StopsCount { get; set; } = 0;
}
