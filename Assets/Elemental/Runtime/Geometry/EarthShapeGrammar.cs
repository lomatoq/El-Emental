using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public enum EarthRockArchetype : byte
    {
        Compact = 0,
        FlatSlab = 1,
        TallCrag = 2,
        OffsetCrown = 3,
        Layered = 4,
        FiveFacet = 5,
        Eroded = 6,
        OreHeavy = 7,
        Wedge = 8,
        Disc = 9,
        Prism = 10,
        BroadCrown = 11
    }

    [Serializable]
    public readonly struct EarthShapeSeed : IEquatable<EarthShapeSeed>
    {
        public EarthShapeSeed(uint value) => Value = value == 0u ? 1u : value;

        public uint Value { get; }

        public static EarthShapeSeed Compose(
            uint worldSeed,
            uint sourceMatterId,
            uint techniqueId,
            uint generation,
            uint variationSalt)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, worldSeed);
            hash = Mix(hash, sourceMatterId);
            hash = Mix(hash, techniqueId);
            hash = Mix(hash, generation);
            hash = Mix(hash, variationSalt);
            return new EarthShapeSeed(hash);
        }

        public bool Equals(EarthShapeSeed other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EarthShapeSeed other && Equals(other);
        public override int GetHashCode() => (int)Value;

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            hash ^= hash >> 13;
            return hash;
        }
    }

    public readonly struct EarthShapeSignature : IEquatable<EarthShapeSignature>
    {
        public EarthShapeSignature(
            EarthRockArchetype archetype,
            byte aspectBucket,
            byte crownBucket,
            uint silhouetteHash)
        {
            Archetype = archetype;
            AspectBucket = aspectBucket;
            CrownBucket = crownBucket;
            SilhouetteHash = silhouetteHash;
        }

        public EarthRockArchetype Archetype { get; }
        public byte AspectBucket { get; }
        public byte CrownBucket { get; }
        public uint SilhouetteHash { get; }

        public bool IsCloseTo(in EarthShapeSignature other) =>
            Archetype == other.Archetype &&
            Math.Abs(AspectBucket - other.AspectBucket) <= 1 &&
            Math.Abs(CrownBucket - other.CrownBucket) <= 1;

        public bool Equals(EarthShapeSignature other) =>
            Archetype == other.Archetype && AspectBucket == other.AspectBucket &&
            CrownBucket == other.CrownBucket && SilhouetteHash == other.SilhouetteHash;
        public override bool Equals(object obj) => obj is EarthShapeSignature other && Equals(other);
        public override int GetHashCode() => unchecked(
            (((int)Archetype * 397) ^ AspectBucket) * 397 ^ CrownBucket ^ (int)SilhouetteHash);
    }

    /// <summary>
    /// Fixed-capacity local history used by gameplay pools. Selection is bounded,
    /// deterministic and allocation-free after construction.
    /// </summary>
    public sealed class EarthShapeDiversityTracker
    {
        private readonly EarthShapeSignature[] _history;
        private int _count;
        private int _cursor;

        public EarthShapeDiversityTracker(int historyLength = 16)
        {
            _history = new EarthShapeSignature[Mathf.Clamp(historyLength, 4, 32)];
        }

        public EarthRockArchetype Select(uint seed, int candidateAttempts = 12)
        {
            int attempts = Mathf.Clamp(candidateAttempts, 1, 32);
            EarthRockArchetype best = (EarthRockArchetype)(seed % EarthRockMeshFactory.ArchetypeCount);
            int bestPenalty = int.MaxValue;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                uint candidateSeed = Hash(seed ^ ((uint)(attempt + 1) * 0x9E3779B9u));
                EarthRockArchetype archetype = (EarthRockArchetype)(candidateSeed % EarthRockMeshFactory.ArchetypeCount);
                EarthShapeSignature signature = EarthRockMeshFactory.Signature(archetype, candidateSeed);
                int penalty = 0;
                for (int index = 0; index < _count; index++)
                {
                    int historyIndex = (_cursor - 1 - index + _history.Length) % _history.Length;
                    if (!_history[historyIndex].IsCloseTo(in signature)) continue;
                    penalty += (_history.Length - index) * 4;
                }
                if (penalty >= bestPenalty) continue;
                bestPenalty = penalty;
                best = archetype;
                if (penalty == 0) break;
            }
            Record(EarthRockMeshFactory.Signature(best, seed));
            return best;
        }

        public void Record(in EarthShapeSignature signature)
        {
            _history[_cursor] = signature;
            _cursor = (_cursor + 1) % _history.Length;
            _count = Mathf.Min(_count + 1, _history.Length);
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    /// <summary>
    /// Small authored grammar of convex rock silhouettes. Each family keeps a flat
    /// support face and collider-safe topology; seeded modifiers affect proportions,
    /// crown and strata without erasing the semantic silhouette.
    /// </summary>
    public static class EarthRockMeshFactory
    {
        public const int ArchetypeCount = 12;

        private readonly struct Parameters
        {
            public Parameters(
                int sides,
                Vector3 aspect,
                float lowerScale,
                float shoulderScale,
                float shoulderY,
                float crownOffset,
                bool flatCrown,
                float phase)
            {
                Sides = sides;
                Aspect = aspect;
                LowerScale = lowerScale;
                ShoulderScale = shoulderScale;
                ShoulderY = shoulderY;
                CrownOffset = crownOffset;
                FlatCrown = flatCrown;
                Phase = phase;
            }

            public int Sides { get; }
            public Vector3 Aspect { get; }
            public float LowerScale { get; }
            public float ShoulderScale { get; }
            public float ShoulderY { get; }
            public float CrownOffset { get; }
            public bool FlatCrown { get; }
            public float Phase { get; }
        }

        public static Mesh Create(EarthRockArchetype archetype, uint seed)
        {
            Parameters parameters = Resolve(archetype, seed);
            int sides = parameters.Sides;
            var logical = new List<Vector3>(sides * 2 + 2);
            int bottomCenter = logical.Count;
            logical.Add(new Vector3(0f, -0.5f, 0f));
            int bottomRing = logical.Count;
            for (int index = 0; index < sides; index++)
            {
                float angle = parameters.Phase + index * Mathf.PI * 2f / sides;
                logical.Add(Scale(new Vector3(
                    Mathf.Cos(angle) * parameters.LowerScale,
                    -0.5f,
                    Mathf.Sin(angle) * parameters.LowerScale), parameters.Aspect));
            }
            int shoulderRing = logical.Count;
            for (int index = 0; index < sides; index++)
            {
                float angle = parameters.Phase + index * Mathf.PI * 2f / sides;
                float strata = 1f + Mathf.Sin(index * 2.17f + Hash01(seed ^ 0x51ED270Bu) * 5f) * 0.035f;
                logical.Add(Scale(new Vector3(
                    Mathf.Cos(angle) * parameters.ShoulderScale * strata,
                    parameters.ShoulderY,
                    Mathf.Sin(angle) * parameters.ShoulderScale * strata), parameters.Aspect));
            }
            int crown = logical.Count;
            if (parameters.FlatCrown)
            {
                logical.Add(Scale(new Vector3(0f, 0.5f, 0f), parameters.Aspect));
            }
            else
            {
                float crownAngle = parameters.Phase + Hash01(seed ^ 0xD1B54A35u) * Mathf.PI * 2f;
                logical.Add(Scale(new Vector3(
                    Mathf.Cos(crownAngle) * parameters.CrownOffset,
                    0.5f,
                    Mathf.Sin(crownAngle) * parameters.CrownOffset), parameters.Aspect));
            }

            var topology = new List<int>(sides * 12);
            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                topology.Add(bottomCenter);
                topology.Add(bottomRing + index);
                topology.Add(bottomRing + next);

                topology.Add(bottomRing + index);
                topology.Add(shoulderRing + index);
                topology.Add(shoulderRing + next);
                topology.Add(bottomRing + index);
                topology.Add(shoulderRing + next);
                topology.Add(bottomRing + next);

                topology.Add(crown);
                topology.Add(shoulderRing + next);
                topology.Add(shoulderRing + index);
            }

            // Split vertices per triangle to preserve the faceted geological read.
            var vertices = new Vector3[topology.Count];
            var triangles = new int[topology.Count];
            var uv = new Vector2[topology.Count];
            for (int index = 0; index < topology.Count; index++)
            {
                Vector3 point = logical[topology[index]];
                vertices[index] = point;
                triangles[index] = index;
                uv[index] = new Vector2(point.x + 0.5f, point.z + 0.5f);
            }
            var mesh = new Mesh
            {
                name = $"EarthRock_{archetype}_{seed:X8}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor,
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ConvexCollider,
                mesh.name,
                mesh.bounds);
            return mesh;
        }

        public static EarthShapeSignature Signature(EarthRockArchetype archetype, uint seed)
        {
            Parameters p = Resolve(archetype, seed);
            float widest = Mathf.Max(p.Aspect.x, p.Aspect.z);
            float narrowest = Mathf.Max(0.001f, Mathf.Min(p.Aspect.x, p.Aspect.z));
            byte aspect = (byte)Mathf.Clamp(Mathf.RoundToInt((widest / narrowest - 1f) * 8f), 0, 15);
            byte crown = (byte)Mathf.Clamp(Mathf.RoundToInt((p.CrownOffset + (p.FlatCrown ? 0.35f : 0f)) * 12f), 0, 15);
            uint hash = Hash(seed ^ ((uint)archetype * 0x9E3779B9u));
            return new EarthShapeSignature(archetype, aspect, crown, hash);
        }

        private static Parameters Resolve(EarthRockArchetype archetype, uint seed)
        {
            float jitterA = Mathf.Lerp(0.94f, 1.06f, Hash01(seed ^ 0xA341316Cu));
            float jitterB = Mathf.Lerp(0.94f, 1.06f, Hash01(seed ^ 0xC8013EA4u));
            float phase = Hash01(seed ^ 0xAD90777Du) * Mathf.PI * 2f;
            return archetype switch
            {
                EarthRockArchetype.FlatSlab => new Parameters(8,
                    new Vector3(1.42f * jitterA, 0.48f, 1.02f * jitterB), 0.96f, 0.88f, 0.50f, 0f, true, phase),
                EarthRockArchetype.TallCrag => new Parameters(6,
                    new Vector3(0.76f * jitterA, 1.48f, 0.82f * jitterB), 0.82f, 1f, 0.08f, 0.18f, false, phase),
                EarthRockArchetype.OffsetCrown => new Parameters(7,
                    new Vector3(1.08f * jitterA, 1.05f, 0.84f * jitterB), 0.86f, 1f, 0.04f, 0.38f, false, phase),
                EarthRockArchetype.Layered => new Parameters(9,
                    new Vector3(1.28f * jitterA, 0.72f, 0.82f * jitterB), 0.92f, 1f, 0.18f, 0.12f, false, phase),
                EarthRockArchetype.FiveFacet => new Parameters(5,
                    new Vector3(1.04f * jitterA, 1.02f, 0.98f * jitterB), 0.84f, 1f, 0.02f, 0.16f, false, phase),
                EarthRockArchetype.Eroded => new Parameters(10,
                    new Vector3(1.04f * jitterA, 0.88f, 1.02f * jitterB), 0.93f, 1f, 0.24f, 0.08f, false, phase),
                EarthRockArchetype.OreHeavy => new Parameters(6,
                    new Vector3(1.26f * jitterA, 1.12f, 0.92f * jitterB), 0.92f, 1f, 0.16f, 0.22f, false, phase),
                EarthRockArchetype.Wedge => new Parameters(5,
                    new Vector3(1.48f * jitterA, 0.66f, 0.72f * jitterB), 0.88f, 1f, 0.10f, 0.34f, false, phase),
                EarthRockArchetype.Disc => new Parameters(10,
                    new Vector3(1.52f * jitterA, 0.38f, 1.18f * jitterB), 0.96f, 1f, 0.26f, 0.04f, false, phase),
                EarthRockArchetype.Prism => new Parameters(4,
                    new Vector3(0.88f * jitterA, 1.22f, 0.86f * jitterB), 0.90f, 1f, 0.20f, 0f, true, phase + Mathf.PI * 0.25f),
                EarthRockArchetype.BroadCrown => new Parameters(8,
                    new Vector3(1.18f * jitterA, 0.92f, 1.20f * jitterB), 0.84f, 1f, 0.36f, 0f, true, phase),
                _ => new Parameters(7,
                    new Vector3(1.00f * jitterA, 0.94f, 0.92f * jitterB), 0.86f, 1f, 0.12f, 0.12f, false, phase)
            };
        }

        private static Vector3 Scale(Vector3 value, Vector3 scale) => new Vector3(
            value.x * scale.x,
            value.y * scale.y,
            value.z * scale.z);

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float Hash01(uint value) => (Hash(value) & 0x00FFFFFFu) / 16777215f;
    }

    public enum EarthWallArchetype : byte
    {
        MonolithicSlab = 0,
        StratifiedRidge = 1,
        ClusteredColumns = 2,
        StaggeredBarricade = 3,
        ArchedChord = 4,
        BrokenCrest = 5,
        ButtressedWall = 6,
        InterlockingPlates = 7
    }

    public readonly struct EarthWallShapeSignature : IEquatable<EarthWallShapeSignature>
    {
        public EarthWallShapeSignature(EarthWallArchetype archetype, byte crestBucket, byte depthBucket)
        {
            Archetype = archetype;
            CrestBucket = crestBucket;
            DepthBucket = depthBucket;
        }

        public EarthWallArchetype Archetype { get; }
        public byte CrestBucket { get; }
        public byte DepthBucket { get; }

        public bool IsCloseTo(in EarthWallShapeSignature other) =>
            Archetype == other.Archetype &&
            Math.Abs(CrestBucket - other.CrestBucket) <= 1 &&
            Math.Abs(DepthBucket - other.DepthBucket) <= 1;

        public bool Equals(EarthWallShapeSignature other) =>
            Archetype == other.Archetype && CrestBucket == other.CrestBucket && DepthBucket == other.DepthBucket;
        public override bool Equals(object obj) => obj is EarthWallShapeSignature other && Equals(other);
        public override int GetHashCode() => ((int)Archetype * 397 ^ CrestBucket) * 397 ^ DepthBucket;
    }

    /// <summary>
    /// Local bounded anti-repeat selector for wall silhouettes. It deliberately owns
    /// its own signature history because a wall duplicate is judged by crest/depth,
    /// not by the boulder aspect/crown buckets.
    /// </summary>
    public sealed class EarthWallShapeDiversityTracker
    {
        private readonly EarthWallShapeSignature[] _history;
        private int _count;
        private int _cursor;

        public EarthWallShapeDiversityTracker(int historyLength = 16) =>
            _history = new EarthWallShapeSignature[Mathf.Clamp(historyLength, 4, 32)];

        public EarthWallArchetype Select(uint seed, int candidateAttempts = 12)
        {
            EarthWallArchetype best = (EarthWallArchetype)(seed % EarthWallMeshFactory.ArchetypeCount);
            EarthWallShapeSignature bestSignature = EarthWallMeshFactory.Signature(best, seed);
            int bestPenalty = int.MaxValue;
            int attempts = Mathf.Clamp(candidateAttempts, 1, 32);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                uint candidateSeed = EarthWallMeshFactory.Hash(seed ^ ((uint)(attempt + 1) * 0x9E3779B9u));
                EarthWallArchetype archetype = (EarthWallArchetype)(candidateSeed % EarthWallMeshFactory.ArchetypeCount);
                EarthWallShapeSignature signature = EarthWallMeshFactory.Signature(archetype, candidateSeed);
                int penalty = 0;
                for (int index = 0; index < _count; index++)
                {
                    int historyIndex = (_cursor - 1 - index + _history.Length) % _history.Length;
                    if (_history[historyIndex].Archetype == signature.Archetype)
                        penalty += _history.Length - index;
                    if (_history[historyIndex].IsCloseTo(in signature))
                        penalty += (_history.Length - index) * 4;
                }
                if (penalty >= bestPenalty) continue;
                best = archetype;
                bestSignature = signature;
                bestPenalty = penalty;
                if (penalty == 0) break;
            }

            Record(in bestSignature);
            return best;
        }

        private void Record(in EarthWallShapeSignature signature)
        {
            _history[_cursor] = signature;
            _cursor = (_cursor + 1) % _history.Length;
            _count = Mathf.Min(_count + 1, _history.Length);
        }
    }

    /// <summary>
    /// Eight semantic wall families with deterministic crest and thickness changes.
    /// The lower edge is kept perfectly planar for chord burial and the visual mesh
    /// stays a closed volume; gameplay support continues to use the stable box proxy.
    /// </summary>
    public static class EarthWallMeshFactory
    {
        public const int ArchetypeCount = 8;

        public static Mesh Create(EarthWallArchetype archetype, uint seed)
        {
            int segments = archetype switch
            {
                EarthWallArchetype.ClusteredColumns => 9,
                EarthWallArchetype.StaggeredBarricade => 8,
                EarthWallArchetype.BrokenCrest => 10,
                EarthWallArchetype.InterlockingPlates => 9,
                _ => 7
            };
            int knotCount = segments + 1;
            var top = new float[knotCount];
            var depth = new float[knotCount];
            float phase = Hash01(seed ^ 0xA341316Cu) * Mathf.PI * 2f;
            float crestJitter = Mathf.Lerp(0.035f, 0.09f, Hash01(seed ^ 0xC8013EA4u));
            float depthJitter = Mathf.Lerp(0.04f, 0.14f, Hash01(seed ^ 0xAD90777Du));
            for (int index = 0; index < knotCount; index++)
            {
                float t = index / (float)segments;
                float wave = Mathf.Sin(phase + index * 1.73f);
                float sharpNoise = Hash01(seed ^ ((uint)(index + 1) * 0x9E3779B9u)) * 2f - 1f;
                top[index] = ResolveTop(archetype, t, index, wave, sharpNoise, crestJitter);
                depth[index] = ResolveDepth(archetype, t, index, wave, sharpNoise, depthJitter);
            }

            var logical = new Vector3[knotCount * 4];
            for (int index = 0; index < knotCount; index++)
            {
                float x = Mathf.Lerp(-0.5f, 0.5f, index / (float)segments);
                float halfDepth = Mathf.Clamp(depth[index], 0.36f, 0.72f) * 0.5f;
                int vertex = index * 4;
                logical[vertex] = new Vector3(x, -0.5f, -halfDepth);
                logical[vertex + 1] = new Vector3(x, top[index], -halfDepth);
                logical[vertex + 2] = new Vector3(x, -0.5f, halfDepth);
                logical[vertex + 3] = new Vector3(x, top[index], halfDepth);
            }

            var topology = new List<int>(segments * 24 + 12);
            for (int index = 0; index < segments; index++)
            {
                int a = index * 4;
                int b = (index + 1) * 4;
                AddQuad(topology, a, a + 1, b + 1, b);       // front -Z
                AddQuad(topology, a + 2, b + 2, b + 3, a + 3); // back +Z
                AddQuad(topology, a + 1, a + 3, b + 3, b + 1); // crest +Y
                AddQuad(topology, a, b, b + 2, a + 2);       // foundation -Y
            }
            AddQuad(topology, 0, 2, 3, 1); // left cap -X
            int last = segments * 4;
            AddQuad(topology, last, last + 1, last + 3, last + 2); // right cap +X

            var vertices = new Vector3[topology.Count];
            var triangles = new int[topology.Count];
            var uv = new Vector2[topology.Count];
            for (int index = 0; index < topology.Count; index++)
            {
                Vector3 point = logical[topology[index]];
                vertices[index] = point;
                triangles[index] = index;
                uv[index] = new Vector2(point.x + 0.5f, point.y + 0.5f);
            }

            var mesh = new Mesh
            {
                name = $"EarthWall_{archetype}_{seed:X8}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor,
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EarthMeshIntegrityGate.ValidateInPlaceOrUseFallback(
                mesh,
                EarthMeshIntegrityPolicy.ClosedHero,
                mesh.name,
                new Bounds(Vector3.zero, Vector3.one));
            return mesh;
        }

        public static EarthWallShapeSignature Signature(EarthWallArchetype archetype, uint seed)
        {
            byte crest = (byte)Mathf.Clamp(Mathf.FloorToInt(Hash01(seed ^ 0xD1B54A35u) * 8f), 0, 7);
            byte depth = (byte)Mathf.Clamp(Mathf.FloorToInt(Hash01(seed ^ 0x94D049BBu) * 8f), 0, 7);
            return new EarthWallShapeSignature(archetype, crest, depth);
        }

        internal static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float ResolveTop(
            EarthWallArchetype archetype,
            float t,
            int index,
            float wave,
            float noise,
            float amount)
        {
            float edge = Mathf.Abs(t * 2f - 1f);
            float value = archetype switch
            {
                EarthWallArchetype.MonolithicSlab => 0.43f + wave * amount * 0.32f,
                EarthWallArchetype.StratifiedRidge => 0.38f + Mathf.Sin(t * Mathf.PI) * 0.12f + wave * amount,
                EarthWallArchetype.ClusteredColumns => 0.28f + ((index % 3) switch { 0 => 0.17f, 1 => 0.08f, _ => 0.22f }) + noise * 0.025f,
                EarthWallArchetype.StaggeredBarricade => 0.32f + (index % 2 == 0 ? 0.16f : 0.04f) + wave * 0.025f,
                EarthWallArchetype.ArchedChord => 0.28f + (1f - edge * edge) * 0.23f + wave * 0.025f,
                EarthWallArchetype.BrokenCrest => 0.38f + noise * amount * 1.45f + (index % 4 == 0 ? 0.08f : 0f),
                EarthWallArchetype.ButtressedWall => 0.39f + (1f - edge) * 0.09f + wave * 0.035f,
                EarthWallArchetype.InterlockingPlates => 0.35f + ((index / 2) % 2 == 0 ? 0.14f : 0.03f) + noise * 0.02f,
                _ => 0.42f
            };
            return Mathf.Clamp(value, 0.22f, 0.58f);
        }

        private static float ResolveDepth(
            EarthWallArchetype archetype,
            float t,
            int index,
            float wave,
            float noise,
            float amount)
        {
            float edge = Mathf.Abs(t * 2f - 1f);
            float value = archetype switch
            {
                EarthWallArchetype.StratifiedRidge => 0.82f + wave * amount,
                EarthWallArchetype.ClusteredColumns => 0.72f + (index % 3 == 0 ? 0.20f : -0.04f),
                EarthWallArchetype.StaggeredBarricade => 0.78f + (index % 2 == 0 ? 0.16f : -0.08f),
                EarthWallArchetype.ArchedChord => 0.76f + (1f - edge) * 0.16f,
                EarthWallArchetype.BrokenCrest => 0.82f + noise * amount,
                EarthWallArchetype.ButtressedWall => 0.76f + edge * 0.38f,
                EarthWallArchetype.InterlockingPlates => 0.74f + ((index / 2) % 2 == 0 ? 0.22f : -0.03f),
                _ => 0.82f + wave * amount * 0.35f
            };
            return Mathf.Clamp(value, 0.68f, 1.16f);
        }

        private static float Hash01(uint value) => (Hash(value) & 0x00FFFFFFu) / 16777215f;

        private static void AddQuad(List<int> indices, int a, int b, int c, int d)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            indices.Add(a);
            indices.Add(c);
            indices.Add(d);
        }
    }
}
