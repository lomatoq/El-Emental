using UnityEngine;
using UnityEngine.VFX;

namespace Elemental.Presentation.Fire
{
    public enum FireVisualBackendSelection { Automatic, GpuVfx, CpuMesh }
    [CreateAssetMenu(menuName = "Elemental/Fire/Visual Profile")]
    public sealed class FireVisualProfile : ScriptableObject
    {
        public FireVisualBackendSelection Backend = FireVisualBackendSelection.Automatic;
        public Material CpuMaterial;
        public bool CoherentBody;
        public Shader CoherentBodyShader;
        [Range(256,512)] public int CpuCapacity = 512;
        public VisualEffectAsset Graph;
        public bool NativeBackendEnabled = true;
        [Min(0)] public float SpawnRate = 320;
        [Min(0.01f)] public float FlameMinWidth = 0.24f;
        [Min(0.01f)] public float FlameMaxWidth = 0.5f;
        [Min(0.1f)] public float FlameMinAspect = 1.4f;
        [Min(0.1f)] public float FlameMaxAspect = 2.3f;
        [Min(0.01f)] public float MinLifetime = 0.45f;
        [Min(0.01f)] public float MaxLifetime = 0.85f;
        [Min(0.001f)] public float ParticleRadius = 0.035f;
        [Min(0)] public float FreeDrag = 1.2f;
        public float FreeLift = 2;
        [Range(0,.2f)] public float EmberFraction=.07f;
        [Min(0.1f)] public float MaximumSpeed = 24;
        [Range(1,4)] public int Substeps = 2;
        [Min(1)] public float LowDetailDistance = 25;
        [Range(0.05f,1)] public float LowDetailRate = 0.5f;
        [Min(0)] public float LightIntensity = 2;
        [Min(0.1f)] public float LightRange = 5;
        public bool IsValid => (Backend != FireVisualBackendSelection.GpuVfx || Graph != null) && MinLifetime > 0 && MaxLifetime >= MinLifetime &&
            MaximumSpeed > 0 && ParticleRadius > 0 && Substeps >= 1 && Substeps <= 4;
    }
}


