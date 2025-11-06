using System.Runtime.InteropServices;
using Telemetry.Domain.Abstractions;

namespace Telemetry.Domain.Accumulators
{
    public class StopAccumulator : IStopAccumulator
    {
        private int _stops = 0;
        private readonly Dictionary<string, bool> movementPerVehicle = [];

        public double CurrentAverage()
        {
            if (_stops == 0) return 0D;
            return _stops / movementPerVehicle.Count;
        }

        public void Observe(string vehicleId, double speed)
        {
            bool moving = speed > 0.1;

            ref bool wasMoving = ref CollectionsMarshal.GetValueRefOrAddDefault(
                movementPerVehicle, vehicleId, out bool exists);

            if (!exists) { wasMoving = moving; return; }

            if (wasMoving && !moving) _stops++;
            wasMoving = moving;
        }

        public void ResetForNewDay()
        {
            movementPerVehicle.Clear();
            _stops = 0;
        }
    }
}
