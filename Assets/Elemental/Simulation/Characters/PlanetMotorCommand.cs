using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    [Serializable]
    public readonly struct PlanetMotorCommand
    {
        public PlanetMotorCommand(uint tick, float2 move, bool jumpPressed)
        {
            Tick = tick;
            Move = math.all(math.isfinite(move))
                ? math.normalizesafe(move) * math.min(1f, math.length(move))
                : float2.zero;
            JumpPressed = jumpPressed;
        }

        public uint Tick { get; }
        public float2 Move { get; }
        public bool JumpPressed { get; }
    }
}
