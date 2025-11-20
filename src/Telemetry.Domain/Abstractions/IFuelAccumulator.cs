using FluentResults;

namespace Telemetry.Domain.Abstractions
{
    public interface IFuelAccumulator
    {
        void Observe(string vehicleId, double fuel);
        double CurrentAverage();
        void ResetForNewDay();
    }
}
