using Elemental.Simulation.Materials;
using UnityEngine;

namespace Elemental.Authoring.Assets
{
    [CreateAssetMenu(menuName = "Elemental/Materials/Material Definition", fileName = "MaterialDefinition")]
    public sealed class MaterialDefinitionAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private ushort materialId = 1;
        [SerializeField, Min(0.001f)] private float density = 1000f;
        [SerializeField, Min(0.001f)] private float thermalCapacity = 4.18f;
        [SerializeField, Min(0f)] private float conductivity = 0.6f;
        [SerializeField] private float meltTemperature;
        [SerializeField] private float boilTemperature = 100f;
        [SerializeField] private float ignitionTemperature = float.PositiveInfinity;
        [SerializeField, Min(0f)] private float latentHeatMelt = 334f;
        [SerializeField, Min(0f)] private float latentHeatVaporize = 2256f;
        [SerializeField, Min(0f)] private float fuelValue;
        [SerializeField] private MaterialTags tags = MaterialTags.Water;

        public void Configure(in MaterialDefinition definition)
        {
            materialId = definition.Id.Value; density = definition.Density;
            thermalCapacity = definition.ThermalCapacity; conductivity = definition.Conductivity;
            meltTemperature = definition.MeltTemperature; boilTemperature = definition.BoilTemperature;
            ignitionTemperature = definition.IgnitionTemperature; latentHeatMelt = definition.LatentHeatMelt;
            latentHeatVaporize = definition.LatentHeatVaporize; fuelValue = definition.FuelValue; tags = definition.Tags;
        }

        public MaterialDefinition Bake() => new MaterialDefinition(
            new MaterialId(materialId), density, thermalCapacity, conductivity,
            meltTemperature, boilTemperature, ignitionTemperature,
            latentHeatMelt, latentHeatVaporize, fuelValue, tags);
    }
}
