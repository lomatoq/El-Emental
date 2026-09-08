using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [DisallowMultipleComponent]
    public sealed class EarthMatterKernelBehaviour : MonoBehaviour
    {
        [SerializeField, Range(128, 8192)] private int capacity = 2048;
        [SerializeField] private EarthMatterMassPolicyAsset massPolicyAsset;
        private EarthMatterMassProfile _massPolicy;
        private bool _massPolicyCaptured;
        public EarthMatterMassPolicyAsset MassPolicyAsset => massPolicyAsset;
        public EarthMatterMassProfile MassPolicy
        {
            get
            {
                if (!_massPolicyCaptured)
                {
                    // Unauthored standalone fixtures retain the deterministic default.
                    // The production installer explicitly binds and validates the asset.
                    _massPolicy = massPolicyAsset != null ? massPolicyAsset.Snapshot : EarthMatterMassProfile.ArenaStone;
                    _massPolicyCaptured = true;
                }
                return _massPolicy;
            }
        }
        public void ConfigureMassPolicy(EarthMatterMassPolicyAsset asset)
        {
            if (Application.isPlaying && _massPolicyCaptured && asset != massPolicyAsset)
                throw new System.InvalidOperationException("Configure the world mass policy before creating earth matter.");
            massPolicyAsset = asset;
            if (!Application.isPlaying) _massPolicyCaptured = false;
        }
        private EarthMatterRegistry _registry;

        public EarthMatterRegistry Registry => _registry ??= new EarthMatterRegistry(capacity);
        public int ActiveRecordCount => Registry.ActiveCount;

        private void Awake() => _registry = new EarthMatterRegistry(capacity);

        public bool TryRegister(EarthMatterIdentity identity, in EarthMatterRecord authored)
        {
            if (identity == null) return false;
            if (identity.MatterId.IsValid && Registry.TryGet(identity.MatterId, out EarthMatterRecord existing))
            {
                if (existing.Phase != EarthMatterPhase.Consumed) return false;
                if (!Registry.TryRecycleConsumed(identity.MatterId, authored, out EarthMatterId recycled))
                    return false;
                identity.AcceptRegistration(this, recycled);
                return true;
            }
            if (!Registry.TryRegister(authored, out EarthMatterId id)) return false;
            identity.AcceptRegistration(this, id);
            return true;
        }

        public bool TryGet(EarthMatterId id, out EarthMatterRecord record) => Registry.TryGet(id, out record);

        public static EarthMatterKernelBehaviour FindOrCreate(Component owner)
        {
            EarthMatterKernelBehaviour existing = FindAnyObjectByType<EarthMatterKernelBehaviour>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            GameObject host = owner != null ? owner.gameObject : new GameObject("Earth Matter Kernel");
            return host.AddComponent<EarthMatterKernelBehaviour>();
        }
    }
}
