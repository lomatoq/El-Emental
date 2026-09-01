using System;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [Serializable]
    internal struct EarthFragmentCapsuleShadowSlot
    {
        [SerializeField] private EarthFragment fragment;
        [SerializeField] private HeroRockCapsuleShadowProducer producer;
        [SerializeField, Min(1)] private uint stableGroupId;

        public EarthFragmentCapsuleShadowSlot(
            EarthFragment configuredFragment,
            HeroRockCapsuleShadowProducer configuredProducer,
            uint configuredStableGroupId)
        {
            fragment = configuredFragment;
            producer = configuredProducer;
            stableGroupId = configuredStableGroupId;
        }

        public EarthFragment Fragment => fragment;
        public HeroRockCapsuleShadowProducer Producer => producer;
        public uint StableGroupId => stableGroupId;
    }

    /// <summary>
    /// Presentation-only listener for the prewarmed hero-rock pool. Runtime physics
    /// publishes lifecycle events; this component owns typed contact-shadow epochs
    /// without making the Runtime assembly depend on rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthFragmentCapsuleShadowPoolPresenter : MonoBehaviour
    {
        public const int MaximumHeroShadowSlots = 12;

        [SerializeField] private EarthFragmentPool pool;
        [SerializeField] private DuelRenderingProfile renderingProfile;
        [SerializeField] private EarthFragmentCapsuleShadowSlot[] slots =
            Array.Empty<EarthFragmentCapsuleShadowSlot>();

        private bool _subscribed;

        public int SlotCount => slots?.Length ?? 0;
        public uint AcceptedAcquireCount { get; private set; }
        public uint RejectedAcquireCount { get; private set; }
        public uint ReleaseCount { get; private set; }

        public void Configure(
            EarthFragmentPool configuredPool,
            DuelRenderingProfile configuredProfile,
            EarthFragment[] prewarmedFragments,
            uint firstStableGroupId)
        {
            Unsubscribe();
            pool = configuredPool;
            renderingProfile = configuredProfile;
            int count = Mathf.Min(
                MaximumHeroShadowSlots,
                prewarmedFragments?.Length ?? 0);
            slots = new EarthFragmentCapsuleShadowSlot[count];
            for (int index = 0; index < count; index++)
            {
                EarthFragment fragment = prewarmedFragments[index];
                if (fragment == null)
                    throw new ArgumentException($"Hero-rock slot {index} is missing its fragment.");
                CapsuleShadowCaster caster =
                    fragment.GetComponent<CapsuleShadowCaster>();
                if (caster == null)
                    caster = fragment.gameObject.AddComponent<CapsuleShadowCaster>();
                caster.ConfigureProxies(new[]
                {
                    new CapsuleShadowProxyBinding(
                        fragment.transform,
                        fragment.transform,
                        Vector3.zero,
                        Vector3.zero,
                        0.5f,
                        0.18f)
                });
                CapsuleShadowCasterBinder binder =
                    fragment.GetComponent<CapsuleShadowCasterBinder>();
                if (binder == null)
                    binder = fragment.gameObject.AddComponent<CapsuleShadowCasterBinder>();
                HeroRockCapsuleShadowProducer producer =
                    fragment.GetComponent<HeroRockCapsuleShadowProducer>();
                if (producer == null)
                    producer = fragment.gameObject.AddComponent<HeroRockCapsuleShadowProducer>();
                if (!producer.Configure(caster, binder))
                    throw new InvalidOperationException(
                        $"Hero-rock slot {index} rejected its capsule binding.");
                uint stableGroupId = firstStableGroupId + (uint)index;
                if (stableGroupId == 0u)
                    throw new ArgumentOutOfRangeException(nameof(firstStableGroupId));
                slots[index] = new EarthFragmentCapsuleShadowSlot(
                    fragment,
                    producer,
                    stableGroupId);
            }
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (pool == null) pool = GetComponent<EarthFragmentPool>();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseAll();
        }

        private void Subscribe()
        {
            if (_subscribed || pool == null) return;
            pool.FragmentAcquired += HandleFragmentAcquired;
            pool.FragmentReleased += HandleFragmentReleased;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (pool != null)
            {
                pool.FragmentAcquired -= HandleFragmentAcquired;
                pool.FragmentReleased -= HandleFragmentReleased;
            }
            _subscribed = false;
        }

        private void HandleFragmentAcquired(EarthFragment fragment)
        {
            int index = FindSlot(fragment);
            if (index < 0 || renderingProfile == null ||
                !renderingProfile.UseCapsuleContactShadows)
                return;
            EarthFragmentCapsuleShadowSlot slot = slots[index];
            if (!HeroRockCapsuleShadowIdentity.TryCreate(
                    CapsuleShadowProducerKind.IntactHeroRock,
                    slot.StableGroupId,
                    fragment.TargetHandle.Generation,
                    out HeroRockCapsuleShadowIdentity identity))
            {
                RejectedAcquireCount++;
                return;
            }
            CapsuleContactShadowRuntimeSettings settings =
                renderingProfile.CapsuleContactShadows.CreateRuntimeSettings();
            if (!slot.Producer.TryAcquire(in identity, in settings, out _) ||
                !CapsuleShadowCaster.CommitGeneration(
                    slot.StableGroupId,
                    identity.Generation))
            {
                slot.Producer.Release();
                RejectedAcquireCount++;
                return;
            }
            AcceptedAcquireCount++;
        }

        private void HandleFragmentReleased(EarthFragment fragment)
        {
            int index = FindSlot(fragment);
            if (index < 0) return;
            if (slots[index].Producer != null && slots[index].Producer.Release())
                ReleaseCount++;
        }

        private int FindSlot(EarthFragment fragment)
        {
            int count = slots?.Length ?? 0;
            for (int index = 0; index < count; index++)
                if (ReferenceEquals(slots[index].Fragment, fragment)) return index;
            return -1;
        }

        private void ReleaseAll()
        {
            int count = slots?.Length ?? 0;
            for (int index = 0; index < count; index++)
                slots[index].Producer?.Release();
        }
    }
}
