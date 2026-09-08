using Elemental.Simulation.Structures;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Earth Matter Mass Policy")]
    public sealed class EarthMatterMassPolicyAsset : ScriptableObject
    {
        [SerializeField, Min(.001f)] private float densityKilogramsPerCubicMetre = 2300f;
        [SerializeField, Min(.001f)] private float referencePhysicalMassKilograms = 230f;
        [SerializeField, Min(.001f)] private float referenceGameplayMassKilograms = 120f;
        [SerializeField, Range(.25f, 1f)] private float compressionExponent = .68f;
        [SerializeField, Min(.001f)] private float minimumGameplayMassKilograms = 12f;
        [SerializeField, Min(.001f)] private float maximumGameplayMassKilograms = 1800f;

        public EarthMatterMassProfile Snapshot => new(densityKilogramsPerCubicMetre,
            referencePhysicalMassKilograms, referenceGameplayMassKilograms, compressionExponent,
            minimumGameplayMassKilograms, maximumGameplayMassKilograms);
    }
}
