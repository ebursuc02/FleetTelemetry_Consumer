namespace Telemetry.Domain.Entities;

public class Record
{
    public required string VehicleId { get; init; }
    public DateTime TsUtc { get; init; }
    public double? SpeedKmh { get; init; }
    public double? FuelPct { get; init; }
    public double? CoolantTempC { get; init; }
    public double? OilTempC { get; init; }
    public double? EngineRpm { get; init; }
    public double? Co2 { get; init; }
}
