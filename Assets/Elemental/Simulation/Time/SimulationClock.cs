using System;
using Elemental.Core.Time;

namespace Elemental.Simulation.Time
{
    public sealed class SimulationClock : ISimulationClock
    {
        private SimulationTick _currentTick;

        public SimulationClock(int tickRate, SimulationTick initialTick = default)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Tick rate must be positive.");
            }

            TickRate = tickRate;
            StepSeconds = 1f / tickRate;
            _currentTick = initialTick;
        }

        public SimulationTick CurrentTick => _currentTick;
        public int TickRate { get; }
        public float StepSeconds { get; }

        public SimulationTick Advance()
        {
            _currentTick = _currentTick.Next();
            return _currentTick;
        }

        public void Reset(SimulationTick tick)
        {
            _currentTick = tick;
        }
    }
}
