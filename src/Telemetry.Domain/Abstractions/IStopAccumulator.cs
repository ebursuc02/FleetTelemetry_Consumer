using FluentResults;

namespace Telemetry.Domain.Abstractions;

public interface IStopAccumulator
{
    void Observe(string vehicleId, double speed);
    double CurrentAverage();
    void ResetForNewDay();
}
