namespace Telemetry.Application.DTOs;

public record RecordDto
{
    public required string VehicleId {  get; init; }
    public DateTime TsUtc {  get; init; }
    public double? SpeedKmh {  get; init; }
    public double? FuelPct {  get; init; }
    public double? CoolantTempC {  get; init; }
    public double? OilTempC { get; init; }
    public double? EngineRpm { get; init; }
    public double? Co2 { get; init; }
    public double? BatteryVoltage { get; init; }
}
