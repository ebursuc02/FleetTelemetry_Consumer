namespace Telemetry.Domain.Entities;

public class KpiRecord
{
    public int RecordCount { get; set; } = 0;
    public double Speed { get; set; } = 0;
    public int SpeedRecords { get; set; } = 0;
    public double OilTempC { get; set; } = 0;
    public int OilRecords { get; set; } = 0;
    public double EngineRpm { get; set; } = 0;
    public int EngineRecords { get; set; } = 0;
    public double EmisionCO2 { get; set; } = 0;
    public int Co2Records { get; set; } = 0;
    public int CoolantHighTemperatureCount { get; set; } = 0;
    public double FuelConsumption { get; set; } = 0;
    public bool IsMoving { get; set; } = true;
    public int StopsCount { get; set; } = 0;
}
