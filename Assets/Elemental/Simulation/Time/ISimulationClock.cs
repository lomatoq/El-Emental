using Elemental.Core.Time;

namespace Elemental.Simulation.Time
{
    public interface ISimulationClock
    {
        SimulationTick CurrentTick { get; }
        int TickRate { get; }
        float StepSeconds { get; }
        SimulationTick Advance();
        void Reset(SimulationTick tick);
    }
}
