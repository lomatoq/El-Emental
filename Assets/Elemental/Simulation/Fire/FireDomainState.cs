using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Fire
{
    public enum FireShape : byte { Capsule, Shell, Vortex }
    public enum FireLifecycle : byte { Retired, Active, Draining }

    public readonly struct FireGroupHandle : IEquatable<FireGroupHandle>
    {
        public readonly int Slot;
        public readonly uint Generation;
        public FireGroupHandle(int slot, uint generation) { Slot = slot; Generation = generation; }
        public bool IsValid => Slot >= 0 && Generation != 0;
        public bool Equals(FireGroupHandle other) => Slot == other.Slot && Generation == other.Generation;
        public override bool Equals(object obj) => obj is FireGroupHandle other && Equals(other);
        public override int GetHashCode() => (Slot * 397) ^ (int)Generation;
        public static bool operator ==(FireGroupHandle a, FireGroupHandle b) => a.Equals(b);
        public static bool operator !=(FireGroupHandle a, FireGroupHandle b) => !a.Equals(b);
    }

    // Namespace distinguishes authored fire surfaces from the existing Earth kinds.
    public readonly struct FireSurfaceHandle : IEquatable<FireSurfaceHandle>
    {
        public readonly uint Namespace, Id, Generation, Revision, Face, Piece;
        public FireSurfaceHandle(uint space, uint id, uint generation, uint revision, uint face, uint piece = 0)
        { Namespace = space; Id = id; Generation = generation; Revision = revision; Face = face; Piece = piece; }
        public bool IsValid => Namespace != 0 && Id != 0 && Generation != 0 && Revision != 0;
        public bool Equals(FireSurfaceHandle other) => Namespace == other.Namespace && Id == other.Id &&
            Generation == other.Generation && Revision == other.Revision && Face == other.Face && Piece == other.Piece;
        public override bool Equals(object obj) => obj is FireSurfaceHandle other && Equals(other);
        public override int GetHashCode() => (int)(Namespace * 397u ^ Id * 31u ^ Generation ^ Revision * 17u ^ Face ^ Piece * 131u);
    }

    public struct FireFieldNode
    {
        public float3 A, B, Flow, Up;
        public float Radius, ShellHalfThickness, Response, Lift, Swirl, NoiseSpeed, NoiseFrequency;
        public float Density, Phase, MaxTargetSpeed;
        public FireShape Shape;
        public bool Active;
        public bool IsValid => math.all(math.isfinite(A)) && math.all(math.isfinite(B)) &&
            math.all(math.isfinite(Flow)) && math.all(math.isfinite(Up)) && math.lengthsq(Up) > 0.5f &&
            math.all(math.isfinite(new float4(Radius, ShellHalfThickness, Response, Lift))) &&
            math.all(math.isfinite(new float4(Swirl, NoiseSpeed, NoiseFrequency, Density))) &&
            math.isfinite(Phase) && math.isfinite(MaxTargetSpeed) && Radius > 0 &&
            ShellHalfThickness >= 0 && Response >= 0 && NoiseSpeed >= 0 && NoiseFrequency >= 0 &&
            Density >= 0 && Density <= 1 && MaxTargetSpeed > 0 && Shape <= FireShape.Vortex;

        public static FireFieldNode Stream(float3 a, float3 b, float3 flow, float3 up) => new FireFieldNode
        {
            A = a, B = b, Flow = flow, Up = math.normalizesafe(up, new float3(0, 1, 0)),
            Radius = 0.45f, ShellHalfThickness = 0.1f, Response = 10, Lift = 1.5f,
            Swirl = 1.5f, NoiseSpeed = 0.9f, NoiseFrequency = 1, Density = 1,
            MaxTargetSpeed = 24, Active = true
        };
    }

    public readonly struct FireWorldSettings
    {
        public readonly int MaximumGroups;
        public readonly float MaximumParticleLifetime, TailMargin, MaximumSpeed;
        public FireWorldSettings(int maximumGroups = 8, float maximumParticleLifetime = 0.85f,
            float tailMargin = 0.1f, float maximumSpeed = 24)
        {
            if (maximumGroups < 1 || maximumGroups > 64 || !math.isfinite(maximumParticleLifetime) ||
                maximumParticleLifetime <= 0 || !math.isfinite(tailMargin) || tailMargin < 0 ||
                !math.isfinite(maximumSpeed) || maximumSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumGroups), "Invalid bounded FireWorld settings.");
            MaximumGroups = maximumGroups; MaximumParticleLifetime = maximumParticleLifetime;
            TailMargin = tailMargin; MaximumSpeed = maximumSpeed;
        }
        public static FireWorldSettings Default => new FireWorldSettings(8, 0.85f, 0.1f, 24);
    }
}
