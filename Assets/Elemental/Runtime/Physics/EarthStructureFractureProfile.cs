using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Structure Fracture Profile", fileName = "EarthStructureFractureProfile")]
    public sealed class EarthStructureFractureProfile : ScriptableObject
    {
        [SerializeField, Range(32, 44)] private int wallCellCount = 40;
        [SerializeField, Range(28, 48)] private int platformCellCount = 36;
        [SerializeField, Range(3, 6)] private int minimumHeightLayers = 3;
        [SerializeField, Range(3f, 8f)] private float minimumVolumeP90P10 = 3f;
        [SerializeField, Range(1, 8)] private int lightImpactPieceLimit = 4;
        [SerializeField, Range(4, 16)] private int mediumImpactPieceLimit = 8;
        [SerializeField, Range(1200f, 5000f)] private float constructionImpactImpulse = 2850f;
        [SerializeField, Range(2f, 6f)] private float heavyImpactMultiplier = 4f;

        public int WallCellCount => wallCellCount;
        public int PlatformCellCount => platformCellCount;
        public int MinimumHeightLayers => minimumHeightLayers;
        public float MinimumVolumeP90P10 => minimumVolumeP90P10;
        public int LightImpactPieceLimit => lightImpactPieceLimit;
        public int MediumImpactPieceLimit => mediumImpactPieceLimit;
        public float ConstructionImpactImpulse => constructionImpactImpulse;
        public float HeavyImpactMultiplier => heavyImpactMultiplier;
    }
}
