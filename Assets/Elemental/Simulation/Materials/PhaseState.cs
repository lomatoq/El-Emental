using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Materials
{
    public enum PhaseKind : byte
    {
        Solid = 1,
        Liquid = 2,
        Gas = 3
    }

    public readonly struct PhaseState
    {
        public PhaseState(MaterialId material, PhaseKind phase, float temperature, float mass, float phaseProgress01 = 0f)
        {
            if (!material.IsValid || !float.IsFinite(temperature) || !float.IsFinite(mass) || mass < 0f ||
                !float.IsFinite(phaseProgress01))
            {
                throw new ArgumentOutOfRangeException();
            }
            Material = material;
            Phase = phase;
            Temperature = temperature;
            Mass = mass;
            PhaseProgress01 = math.saturate(phaseProgress01);
        }

        public MaterialId Material { get; }
        public PhaseKind Phase { get; }
        public float Temperature { get; }
        public float Mass { get; }
        public float PhaseProgress01 { get; }

        public PhaseState WithMass(float mass) => new PhaseState(Material, Phase, Temperature, mass, PhaseProgress01);
        public PhaseState WithTemperature(float temperature) => new PhaseState(Material, Phase, temperature, Mass, PhaseProgress01);
    }

    public readonly struct PhaseTransitionResult
    {
        public PhaseTransitionResult(PhaseState state, float appliedEnergy, float rejectedEnergy, bool phaseChanged)
        {
            State = state;
            AppliedEnergy = appliedEnergy;
            RejectedEnergy = rejectedEnergy;
            PhaseChanged = phaseChanged;
        }
        public PhaseState State { get; }
        public float AppliedEnergy { get; }
        public float RejectedEnergy { get; }
        public bool PhaseChanged { get; }
    }

    public static class PhaseTransitionMath
    {
        public static PhaseTransitionResult ApplyEnergy(
            in PhaseState source,
            in MaterialDefinition material,
            float energy,
            float hysteresis = 2f,
            float maximumAbsoluteEnergy = 100000f)
        {
            if (!float.IsFinite(energy) || source.Mass <= 0f)
            {
                return new PhaseTransitionResult(source, 0f, energy, false);
            }

            float bounded = math.clamp(energy, -maximumAbsoluteEnergy, maximumAbsoluteEnergy);
            float capacity = math.max(0.0001f, material.ThermalCapacity * source.Mass);
            float meltLatent = material.LatentHeatMelt * source.Mass;
            float vaporLatent = material.LatentHeatVaporize * source.Mass;
            float enthalpy = ToEnthalpy(in source, in material, capacity, meltLatent, vaporLatent) + bounded;
            PhaseState state = FromEnthalpy(
                source.Material,
                source.Mass,
                enthalpy,
                in material,
                capacity,
                meltLatent,
                vaporLatent,
                hysteresis,
                source.Phase);
            return new PhaseTransitionResult(state, bounded, energy - bounded, state.Phase != source.Phase);
        }

        private static float ToEnthalpy(
            in PhaseState state,
            in MaterialDefinition material,
            float capacity,
            float meltLatent,
            float vaporLatent)
        {
            switch (state.Phase)
            {
                case PhaseKind.Solid:
                    return (capacity * state.Temperature) + (meltLatent * state.PhaseProgress01);
                case PhaseKind.Liquid:
                    return (capacity * state.Temperature) + meltLatent + (vaporLatent * state.PhaseProgress01);
                default:
                    return (capacity * state.Temperature) + meltLatent + vaporLatent;
            }
        }

        private static PhaseState FromEnthalpy(
            MaterialId id,
            float mass,
            float enthalpy,
            in MaterialDefinition material,
            float capacity,
            float meltLatent,
            float vaporLatent,
            float hysteresis,
            PhaseKind previousPhase)
        {
            float meltTemperature = previousPhase == PhaseKind.Liquid && enthalpy < capacity * material.MeltTemperature + meltLatent
                ? material.MeltTemperature - hysteresis
                : material.MeltTemperature;
            float boilTemperature = previousPhase == PhaseKind.Gas && enthalpy < capacity * material.BoilTemperature + meltLatent + vaporLatent
                ? material.BoilTemperature - hysteresis
                : material.BoilTemperature;
            float meltStart = capacity * meltTemperature;
            if (enthalpy < meltStart)
            {
                return new PhaseState(id, PhaseKind.Solid, enthalpy / capacity, mass);
            }
            if (enthalpy < meltStart + meltLatent)
            {
                return new PhaseState(id, PhaseKind.Solid, meltTemperature, mass,
                    meltLatent <= 0f ? 1f : (enthalpy - meltStart) / meltLatent);
            }
            float boilStart = (capacity * boilTemperature) + meltLatent;
            if (enthalpy < boilStart)
            {
                return new PhaseState(id, PhaseKind.Liquid, (enthalpy - meltLatent) / capacity, mass);
            }
            if (enthalpy < boilStart + vaporLatent)
            {
                return new PhaseState(id, PhaseKind.Liquid, boilTemperature, mass,
                    vaporLatent <= 0f ? 1f : (enthalpy - boilStart) / vaporLatent);
            }
            return new PhaseState(id, PhaseKind.Gas, (enthalpy - meltLatent - vaporLatent) / capacity, mass);
        }
    }
}
