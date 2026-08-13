using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Materials
{
    public enum ReactionKind : byte
    {
        None = 0,
        Freeze = 1,
        Vaporize = 2,
        ThermalShock = 3,
        SteamDispersal = 4,
        Ignition = 5
    }

    public readonly struct ReactionContext
    {
        public ReactionContext(
            PhaseState phase,
            MaterialDefinition material,
            float temperatureDelta,
            float coolingRate,
            float airSpeed,
            float oxygen01)
        {
            Phase = phase; Material = material; TemperatureDelta = temperatureDelta;
            CoolingRate = coolingRate; AirSpeed = airSpeed; Oxygen01 = math.saturate(oxygen01);
        }
        public PhaseState Phase { get; }
        public MaterialDefinition Material { get; }
        public float TemperatureDelta { get; }
        public float CoolingRate { get; }
        public float AirSpeed { get; }
        public float Oxygen01 { get; }
    }

    public readonly struct ReactionResult
    {
        public ReactionResult(ReactionKind kind, float energy, float pressureImpulse, float severity)
        {
            Kind = kind; Energy = energy; PressureImpulse = pressureImpulse; Severity = math.saturate(severity);
        }
        public ReactionKind Kind { get; }
        public float Energy { get; }
        public float PressureImpulse { get; }
        public float Severity { get; }
    }

    public sealed class ReactionResolver
    {
        public ReactionResult Resolve(in ReactionContext context)
        {
            PhaseState phase = context.Phase;
            MaterialDefinition material = context.Material;
            float targetTemperature = phase.Temperature + context.TemperatureDelta;
            if ((material.Tags & MaterialTags.Water) != 0)
            {
                if (phase.Phase == PhaseKind.Liquid && targetTemperature <= material.MeltTemperature - 2f)
                {
                    return new ReactionResult(ReactionKind.Freeze, -material.LatentHeatMelt * phase.Mass, 0f, 1f);
                }
                if (phase.Phase != PhaseKind.Gas && targetTemperature >= material.BoilTemperature + 2f)
                {
                    float severity = math.saturate((targetTemperature - material.BoilTemperature) / 100f);
                    return new ReactionResult(
                        ReactionKind.Vaporize,
                        material.LatentHeatVaporize * phase.Mass,
                        math.min(30f, 5f + (severity * 25f)),
                        severity);
                }
                if (phase.Phase == PhaseKind.Gas && context.AirSpeed >= 8f)
                {
                    return new ReactionResult(ReactionKind.SteamDispersal, 0f, math.min(12f, context.AirSpeed), context.AirSpeed / 30f);
                }
            }
            if ((material.Tags & MaterialTags.Brittle) != 0 && phase.Temperature > 250f && context.CoolingRate >= 80f)
            {
                return new ReactionResult(ReactionKind.ThermalShock, 0f, math.min(25f, context.CoolingRate * 0.12f), context.CoolingRate / 300f);
            }
            if ((material.Tags & MaterialTags.Fuel) != 0 && targetTemperature >= material.IgnitionTemperature && context.Oxygen01 >= 0.15f)
            {
                return new ReactionResult(ReactionKind.Ignition, material.FuelValue * phase.Mass, 4f, context.Oxygen01);
            }
            return default;
        }
    }
}
