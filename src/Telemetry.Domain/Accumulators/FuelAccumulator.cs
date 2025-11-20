using Telemetry.Domain.Abstractions;

namespace Telemetry.Domain.Accumulators;

public class FuelAccumulator : IFuelAccumulator
{
    private readonly Dictionary<string, (double lastRec, double consumption)> fuelConsumptionPerVehicle = [];
    public double CurrentAverage()
    {
        if (fuelConsumptionPerVehicle.Count == 0) return 0d;
        return fuelConsumptionPerVehicle.Sum(item => item.Value.consumption) / fuelConsumptionPerVehicle.Count;
    }

    public void Observe(string vehicleId, double fuel)
    {
        if (fuelConsumptionPerVehicle.TryGetValue(vehicleId, out var e))
            fuelConsumptionPerVehicle[vehicleId] = (fuel, e.consumption + Math.Max(0, e.lastRec - fuel));
        else
            fuelConsumptionPerVehicle[vehicleId] = (fuel, 0.0);
    }

    public void ResetForNewDay()
        => fuelConsumptionPerVehicle.Clear();

}
