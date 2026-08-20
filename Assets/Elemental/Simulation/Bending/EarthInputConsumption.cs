using System;

namespace Elemental.Simulation.Bending
{
    [Flags]
    public enum EarthInputConsumption : ushort
    {
        None = 0,
        Cancel = 1 << 0,
        Move = 1 << 1,
        Modifier = 1 << 2,
        Jump = 1 << 3,
        Primary = 1 << 4,
        Force = 1 << 5,
        Field = 1 << 6,
        Parameter = 1 << 7
    }
}
