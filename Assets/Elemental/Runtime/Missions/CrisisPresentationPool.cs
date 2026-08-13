using Elemental.Simulation.Missions;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Missions
{
    [DisallowMultipleComponent]
    public sealed class CrisisPresentationPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] pooledEffects;
        private int _cursor;
        public int ShownCount { get; private set; }

        public void Configure(ParticleSystem[] effects) => pooledEffects = effects;

        public void Show(in CrisisEvent crisis)
        {
            if (pooledEffects == null || pooledEffects.Length == 0) return;
            ParticleSystem effect = pooledEffects[_cursor++ % pooledEffects.Length];
            if (effect == null) return;
            float3 p = crisis.Position;
            effect.transform.position = new Vector3(p.x, p.y, p.z);
            ParticleSystem.MainModule main = effect.main;
            main.startColor = ColorFor(crisis.Kind);
            effect.Emit(Mathf.Clamp(Mathf.RoundToInt(4f + crisis.Severity * 12f), 4, 16));
            ShownCount++;
        }

        private static Color ColorFor(CrisisKind kind)
        {
            switch (kind)
            {
                case CrisisKind.LavaAdvance: return new Color(1f, 0.12f, 0.02f);
                case CrisisKind.SmokeHazard: return new Color(0.28f, 0.28f, 0.3f);
                case CrisisKind.StructuralFailure: return new Color(0.75f, 0.45f, 0.2f);
                case CrisisKind.CivilianPanic: return new Color(1f, 0.8f, 0.12f);
                case CrisisKind.BlockedRoute: return new Color(0.6f, 0.2f, 0.1f);
                default: return new Color(1f, 0.25f, 0.6f);
            }
        }
    }
}
