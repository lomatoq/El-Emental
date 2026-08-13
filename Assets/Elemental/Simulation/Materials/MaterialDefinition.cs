using System;

namespace Elemental.Simulation.Materials
{
    public readonly struct MaterialId : IEquatable<MaterialId>
    {
        public MaterialId(ushort value) => Value = value;
        public ushort Value { get; }
        public bool IsValid => Value != 0;
        public bool Equals(MaterialId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is MaterialId other && Equals(other);
        public override int GetHashCode() => Value;
    }

    [Flags]
    public enum MaterialTags : ushort
    {
        None = 0,
        Water = 1 << 0,
        Fuel = 1 << 1,
        Ignitable = 1 << 2,
        Brittle = 1 << 3,
        Permeable = 1 << 4
    }

    public readonly struct MaterialDefinition
    {
        public MaterialDefinition(
            MaterialId id,
            float density,
            float thermalCapacity,
            float conductivity,
            float meltTemperature,
            float boilTemperature,
            float ignitionTemperature,
            float latentHeatMelt,
            float latentHeatVaporize,
            float fuelValue,
            MaterialTags tags)
        {
            if (!id.IsValid || density <= 0f || thermalCapacity <= 0f || conductivity < 0f ||
                boilTemperature <= meltTemperature || latentHeatMelt < 0f || latentHeatVaporize < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Material thresholds and energy values must be bounded.");
            }

            Id = id;
            Density = density;
            ThermalCapacity = thermalCapacity;
            Conductivity = conductivity;
            MeltTemperature = meltTemperature;
            BoilTemperature = boilTemperature;
            IgnitionTemperature = ignitionTemperature;
            LatentHeatMelt = latentHeatMelt;
            LatentHeatVaporize = latentHeatVaporize;
            FuelValue = fuelValue;
            Tags = tags;
        }

        public MaterialId Id { get; }
        public float Density { get; }
        public float ThermalCapacity { get; }
        public float Conductivity { get; }
        public float MeltTemperature { get; }
        public float BoilTemperature { get; }
        public float IgnitionTemperature { get; }
        public float LatentHeatMelt { get; }
        public float LatentHeatVaporize { get; }
        public float FuelValue { get; }
        public MaterialTags Tags { get; }

        public static MaterialDefinition Water => new MaterialDefinition(
            new MaterialId(1), 1000f, 4.18f, 0.6f, 0f, 100f, float.PositiveInfinity,
            334f, 2256f, 0f, MaterialTags.Water);

        public static MaterialDefinition BrittleRock => new MaterialDefinition(
            new MaterialId(2), 2500f, 0.84f, 1.8f, 900f, 2500f, float.PositiveInfinity,
            400f, 4000f, 0f, MaterialTags.Brittle);

        public static MaterialDefinition Fuel => new MaterialDefinition(
            new MaterialId(3), 650f, 1.7f, 0.15f, 120f, 450f, 230f,
            80f, 900f, 18000f, MaterialTags.Fuel | MaterialTags.Ignitable);
    }
}
