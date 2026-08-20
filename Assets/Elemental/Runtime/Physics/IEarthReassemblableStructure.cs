using Elemental.Simulation.Structures;

namespace Elemental.Runtime.Physics
{
    public interface IEarthRepairController
    {
        bool IsRepairing { get; }
        bool TryBeginRepair(uint tick);
        bool TryBeginRepair(uint tick, float targetProgress01);
        bool SetTargetProgress(float targetProgress01, uint tick = 0u);
        void Interrupt(EarthRepairInterruptReason reason, uint tick);
    }

    public interface IEarthReassemblableStructure : IEarthFractureSource
    {
        IEarthRepairController RepairController { get; }
    }
}
