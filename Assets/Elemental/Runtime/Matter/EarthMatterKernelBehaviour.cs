using Elemental.Simulation.Matter;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [DisallowMultipleComponent]
    public sealed class EarthMatterKernelBehaviour : MonoBehaviour
    {
        [SerializeField, Range(128, 8192)] private int capacity = 2048;
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
