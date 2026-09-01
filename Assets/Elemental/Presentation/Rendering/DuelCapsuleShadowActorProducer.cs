using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Explicit scene-lifecycle acquisition seam for the two persistent duel
    /// actors. The caster itself owns no serialized identity and cannot resurrect
    /// from a component toggle; a caller must invoke Acquire again after a later
    /// representation acquisition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelCapsuleShadowActorProducer : MonoBehaviour
    {
        [SerializeField] private CapsuleShadowCaster caster;
        [SerializeField] private CapsuleShadowCasterBinder binder;
        [SerializeField] private CapsuleShadowProducerKind producerKind;
        [SerializeField, Min(1)] private uint stableGroupId;
        [SerializeField, Min(1)] private uint generation = 1u;

        public bool IsAcquired { get; private set; }
        public uint StableGroupId => stableGroupId;
        public uint Generation => generation;
        public CapsuleShadowProducerKind ProducerKind => producerKind;

        public void Configure(
            CapsuleShadowCaster configuredCaster,
            CapsuleShadowProducerKind configuredProducerKind,
            uint configuredStableGroupId,
            uint configuredGeneration)
        {
            Release();
            caster = configuredCaster;
            producerKind = configuredProducerKind;
            stableGroupId = configuredStableGroupId;
            generation = configuredGeneration;
            if (binder == null) binder = GetComponent<CapsuleShadowCasterBinder>();
            if (binder == null) binder = gameObject.AddComponent<CapsuleShadowCasterBinder>();
        }

        private void Start() => Acquire();

        private void LateUpdate()
        {
            // Scene/domain startup can recreate the fixed shadow buffer after an
            // early component callback. Re-admit the persistent duel actor until
            // ownership has actually committed; once acquired this is a cheap
            // branch and performs no registration work or allocation.
            if (!IsAcquired)
                Acquire();
        }

        private void OnDisable() => Release();

        public bool Acquire()
        {
            if (IsAcquired) return true;
            if (caster == null || binder == null || stableGroupId == 0u || generation == 0u)
                return false;
            if (!binder.TryAcquire(caster, producerKind, stableGroupId, generation))
                return false;
            if (!binder.CommitGeneration(stableGroupId, generation))
            {
                binder.ReleaseAcquisition(caster);
                return false;
            }
            IsAcquired = true;
            return true;
        }

        public void Release()
        {
            if (binder != null && caster != null)
                binder.ReleaseAcquisition(caster);
            IsAcquired = false;
        }
    }
}
