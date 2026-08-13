using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Elemental.Simulation.Magic
{
    public enum ElementId : byte
    {
        Earth = 1,
        Air = 2,
        Fire = 3,
        Water = 4
    }

    [Serializable]
    public readonly struct MagicCommand
    {
        public MagicCommand(
            uint tick,
            uint casterId,
            ElementId element,
            AbilityId ability,
            float3 origin,
            float3 aim,
            IReadOnlyList<float3> path,
            float intensity,
            uint modifiers,
            uint seed)
        {
            if (!ability.IsValid)
            {
                throw new ArgumentException("Ability ID must be valid.", nameof(ability));
            }

            if (!math.all(math.isfinite(origin)) || !math.all(math.isfinite(aim)))
            {
                throw new ArgumentException("Origin and aim must be finite.");
            }

            Tick = tick;
            CasterId = casterId;
            Element = element;
            Ability = ability;
            Origin = origin;
            Aim = math.normalizesafe(aim, new float3(0f, 1f, 0f));
            Intensity = math.saturate(intensity);
            Modifiers = modifiers;
            Seed = seed;

            FixedList512Bytes<float3> fixedPath = default;
            if (path != null)
            {
                if (path.Count > fixedPath.Capacity)
                {
                    throw new ArgumentOutOfRangeException(nameof(path), $"Path supports at most {fixedPath.Capacity} points.");
                }

                for (int index = 0; index < path.Count; index++)
                {
                    float3 point = path[index];
                    if (!math.all(math.isfinite(point)))
                    {
                        throw new ArgumentException("Path points must be finite.", nameof(path));
                    }

                    fixedPath.Add(point);
                }
            }

            Path = fixedPath;
        }

        public uint Tick { get; }
        public uint CasterId { get; }
        public ElementId Element { get; }
        public AbilityId Ability { get; }
        public float3 Origin { get; }
        public float3 Aim { get; }
        public FixedList512Bytes<float3> Path { get; }
        public float Intensity { get; }
        public uint Modifiers { get; }
        public uint Seed { get; }
    }
}
