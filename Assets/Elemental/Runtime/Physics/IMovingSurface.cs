using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public interface IMovingSurface
    {
        uint SurfaceId { get; }
        Vector3 SurfaceVelocity { get; }
        Vector3 SurfaceUp { get; }
        bool IsEmerging { get; }
        SupportFrameSnapshot SupportFrame { get; }
        MovingSupportSnapshot Snapshot { get; }
    }
}
