using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Armor Profile", fileName = "EarthArmorProfile")]
    public sealed class EarthArmorProfile : ScriptableObject
    {
        public const int MaximumPieceCount = 96;

        [SerializeField, Range(56, MaximumPieceCount)]
        private int pieceCount = MaximumPieceCount;
        [SerializeField] private EarthArmorShellDefinition shellDefinition;
        [SerializeField, Range(0.15f, 0.6f)] private float assemblySeconds = 0.30f;
        [SerializeField, Range(0.5f, 1.2f)] private float bodyRadius = 0.78f;
        [SerializeField, Range(0.02f, 0.22f)] private float bodySurfaceOffset = 0.028f;
        [SerializeField, Range(0.9f, 1.35f)] private float bodyPlateScaleMultiplier = 1.12f;
        [SerializeField, Range(0.9f, 2.2f)] private float expandedPlateScaleMultiplier = 1.02f;
        [SerializeField, Range(1.2f, 3f)] private float domeRadius = 2.5f;
        [SerializeField, Range(2f, 4f)] private float orbitRadius = 3.2f;
        [SerializeField, Range(0.04f, 0.30f)] private float phasePerWheelStep = 0.14f;
        [SerializeField, Range(0.1f, 0.8f)] private float overscrollConfirmationSeconds = 0.35f;
        [SerializeField, Range(8f, 30f)] private float minimumBurstSpeed = 16f;
        [SerializeField, Range(8f, 32f)] private float maximumBurstSpeed = 24f;
        [SerializeField, Range(18f, 38f)] private float aimedProjectileSpeed = 31f;
        [SerializeField, Range(0.05f, 0.3f)] private float automaticFireInterval = 0.13f;
        [SerializeField, Range(0.5f, 4f)] private float debrisRestSeconds = 1.2f;
        [SerializeField, Range(0.3f, 3f)] private float debrisShrinkSeconds = 1.1f;

        public int PieceCount => pieceCount;
        public EarthArmorShellDefinition ShellDefinition => shellDefinition;
        public void ConfigureShellDefinition(EarthArmorShellDefinition definition) => shellDefinition = definition;
        public float AssemblySeconds => assemblySeconds;
        public float BodyRadius => bodyRadius;
        public float BodySurfaceOffset => bodySurfaceOffset;
        public float BodyPlateScaleMultiplier => bodyPlateScaleMultiplier;
        public float ExpandedPlateScaleMultiplier => expandedPlateScaleMultiplier;
        public float DomeRadius => domeRadius;
        public float OrbitRadius => orbitRadius;
        public float MinimumBurstSpeed => minimumBurstSpeed;
        public float MaximumBurstSpeed => maximumBurstSpeed;
        public float AimedProjectileSpeed => aimedProjectileSpeed;
        public float AutomaticFireInterval => automaticFireInterval;
        public float DebrisRestSeconds => debrisRestSeconds;
        public float DebrisShrinkSeconds => debrisShrinkSeconds;
        public EarthArmorProfileData Data => new EarthArmorProfileData(
            phasePerWheelStep, overscrollConfirmationSeconds);
    }
}
