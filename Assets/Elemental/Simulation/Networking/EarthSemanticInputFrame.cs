using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    [Flags]
    public enum EarthInputBits : uint
    {
        None = 0, Primary = 1, Force = 2, Field = 4, Modifier = 8, Jump = 16,
        Cancel = 32, Shoulder = 64, AbilityOne = 128, AbilityTwo = 256, AbilityThree = 512, AbilityFour = 1024
    }
    public struct EarthSemanticInputFrame
    {
        public uint Sequence, Tick;
        public EarthInputBits Held, Pressed, Released;
        public float2 PointerViewport, Move;
        public float Scroll, FieldOfView, Aspect;
        public float3 CameraPosition;
        public quaternion CameraRotation;
        public bool Valid => Sequence != 0 && ((uint)(Held | Pressed | Released) & ~2047u) == 0 &&
            math.all(math.isfinite(PointerViewport)) && math.all(PointerViewport >= 0) && math.all(PointerViewport <= 1) &&
            math.all(math.isfinite(Move)) && math.lengthsq(Move) <= 1.01f && math.isfinite(Scroll) && math.abs(Scroll) <= 240 &&
            math.all(math.isfinite(CameraPosition)) && math.all(math.isfinite(CameraRotation.value)) &&
            math.abs(math.lengthsq(CameraRotation.value) - 1) < .01f &&
            math.isfinite(FieldOfView) && FieldOfView >= 20 && FieldOfView <= 110 &&
            math.isfinite(Aspect) && Aspect >= .4f && Aspect <= 4f;
    }
}
