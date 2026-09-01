using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public enum HeroRockCapsuleShadowAcquireFailure : byte
    {
        None = 0,
        InvalidIdentity = 1,
        UnsupportedProducer = 2,
        InvalidDiameter = 3,
        BelowMinimumDiameter = 4,
        MissingBinding = 5,
        InactiveProducer = 6,
        BufferRejected = 7
    }

    /// <summary>
    /// Typed presentation identity for one stable pooled rock slot or one stable
    /// fracture group at a specific acquisition/representation generation.
    /// </summary>
    public readonly struct HeroRockCapsuleShadowIdentity
    {
        internal HeroRockCapsuleShadowIdentity(
            CapsuleShadowProducerKind producerKind,
            uint stableGroupId,
            uint generation)
        {
            ProducerKind = producerKind;
            StableGroupId = stableGroupId;
            Generation = generation;
        }

        public CapsuleShadowProducerKind ProducerKind { get; }
        public uint StableGroupId { get; }
        public uint Generation { get; }
        public bool IsValid => StableGroupId != 0u &&
            HeroRockCapsuleShadowProducerPolicy.IsSupportedProducer(ProducerKind);

        public static bool TryCreate(
            CapsuleShadowProducerKind producerKind,
            uint stableGroupId,
            uint generation,
            out HeroRockCapsuleShadowIdentity identity)
        {
            identity = default;
            if (stableGroupId == 0u ||
                !HeroRockCapsuleShadowProducerPolicy.IsSupportedProducer(producerKind))
                return false;
            identity = new HeroRockCapsuleShadowIdentity(
                producerKind,
                stableGroupId,
                generation);
            return true;
        }
    }

    /// <summary>
    /// Pure admission policy for the hero-rock presentation adapter. Characters,
    /// cosmetic debris, VFX, invalid identities, and sub-threshold fragments fail
    /// closed before reaching the shared capsule buffer.
    /// </summary>
    public static class HeroRockCapsuleShadowProducerPolicy
    {
        public static bool IsSupportedProducer(CapsuleShadowProducerKind producerKind)
        {
            return producerKind == CapsuleShadowProducerKind.IntactHeroRock ||
                producerKind == CapsuleShadowProducerKind.LargeActiveFracture;
        }

        public static bool TryAdmit(
            in HeroRockCapsuleShadowIdentity identity,
            float worldDiameter,
            in CapsuleContactShadowRuntimeSettings settings,
            out CapsuleShadowCasterClass classification,
            out HeroRockCapsuleShadowAcquireFailure failure)
        {
            classification = CapsuleShadowCasterClass.Other;
            if (identity.StableGroupId == 0u)
            {
                failure = HeroRockCapsuleShadowAcquireFailure.InvalidIdentity;
                return false;
            }
            if (!IsSupportedProducer(identity.ProducerKind) ||
                !CapsuleShadowOwnershipPolicy.TryResolveClassification(
                    identity.ProducerKind,
                    out classification))
            {
                failure = HeroRockCapsuleShadowAcquireFailure.UnsupportedProducer;
                return false;
            }
            if (!float.IsFinite(worldDiameter) || worldDiameter <= 0f)
            {
                failure = HeroRockCapsuleShadowAcquireFailure.InvalidDiameter;
                return false;
            }
            if (!CapsuleShadowCasterPolicy.IsIncluded(
                    classification,
                    worldDiameter,
                    settings))
            {
                failure = HeroRockCapsuleShadowAcquireFailure.BelowMinimumDiameter;
                return false;
            }

            failure = HeroRockCapsuleShadowAcquireFailure.None;
            return true;
        }
    }

    /// <summary>
    /// Thin, explicit pool lifecycle adapter for large hero rocks and active
    /// fracture pieces. It never acquires on enable and releases both the caster
    /// handle and committed generation epoch on pool return.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleShadowCaster))]
    public sealed class HeroRockCapsuleShadowProducer : MonoBehaviour
    {
        private static readonly ProfilerMarker LifecycleMarker =
            new ProfilerMarker("Elemental.Rendering.HeroRockCapsuleLifecycle");

        [SerializeField] private CapsuleShadowCaster caster;
        [SerializeField] private CapsuleShadowCasterBinder binder;

        private bool _isAcquired;
        private HeroRockCapsuleShadowIdentity _identity;

        public bool IsAcquired => _isAcquired;
        public bool IsActiveGeneration => _isAcquired && caster != null &&
            caster.IsActiveGeneration;
        public HeroRockCapsuleShadowIdentity Identity => _identity;
        public HeroRockCapsuleShadowAcquireFailure LastFailure { get; private set; }
        public uint AcquireAttemptCount { get; private set; }
        public uint SuccessfulAcquireCount { get; private set; }
        public uint RejectedAcquireCount { get; private set; }
        public uint ReleaseCount { get; private set; }

        public bool Configure(
            CapsuleShadowCaster configuredCaster,
            CapsuleShadowCasterBinder configuredBinder)
        {
            using (LifecycleMarker.Auto())
            {
                ReleaseInternal(true);
                caster = configuredCaster;
                binder = configuredBinder;
                if (caster != null && binder != null)
                {
                    LastFailure = HeroRockCapsuleShadowAcquireFailure.None;
                    return true;
                }

                LastFailure = HeroRockCapsuleShadowAcquireFailure.MissingBinding;
                return false;
            }
        }

        public bool TryAcquire(
            in HeroRockCapsuleShadowIdentity identity,
            in CapsuleContactShadowRuntimeSettings settings,
            out HeroRockCapsuleShadowAcquireFailure failure)
        {
            using (LifecycleMarker.Auto())
            {
                AcquireAttemptCount = Increment(AcquireAttemptCount);
                if (!identity.IsValid)
                {
                    HeroRockCapsuleShadowAcquireFailure invalidFailure =
                        identity.StableGroupId == 0u
                            ? HeroRockCapsuleShadowAcquireFailure.InvalidIdentity
                            : HeroRockCapsuleShadowAcquireFailure.UnsupportedProducer;
                    ReleaseInternal(true);
                    return Reject(invalidFailure, out failure);
                }
                if (caster == null || binder == null)
                {
                    ReleaseInternal(true);
                    return Reject(
                        HeroRockCapsuleShadowAcquireFailure.MissingBinding,
                        out failure);
                }
                if (!isActiveAndEnabled || !caster.isActiveAndEnabled ||
                    !gameObject.activeInHierarchy)
                {
                    ReleaseInternal(true);
                    return Reject(
                        HeroRockCapsuleShadowAcquireFailure.InactiveProducer,
                        out failure);
                }

                float worldDiameter = caster.EstimateWorldDiameter();
                if (!HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                        identity,
                        worldDiameter,
                        settings,
                        out _,
                        out HeroRockCapsuleShadowAcquireFailure admissionFailure))
                {
                    ReleaseInternal(true);
                    return Reject(admissionFailure, out failure);
                }

                if (_isAcquired && caster.IsRegistered && SameIdentity(_identity, identity))
                {
                    SuccessfulAcquireCount = Increment(SuccessfulAcquireCount);
                    LastFailure = HeroRockCapsuleShadowAcquireFailure.None;
                    failure = HeroRockCapsuleShadowAcquireFailure.None;
                    return true;
                }

                ReleaseInternal(true);

                if (!binder.TryAcquire(
                        caster,
                        identity.ProducerKind,
                        identity.StableGroupId,
                        identity.Generation))
                {
                    binder.ReleaseAcquisition(caster);
                    return Reject(
                        HeroRockCapsuleShadowAcquireFailure.BufferRejected,
                        out failure);
                }

                _identity = identity;
                _isAcquired = true;
                SuccessfulAcquireCount = Increment(SuccessfulAcquireCount);
                LastFailure = HeroRockCapsuleShadowAcquireFailure.None;
                failure = HeroRockCapsuleShadowAcquireFailure.None;
                return true;
            }
        }

        public bool Release()
        {
            using (LifecycleMarker.Auto())
            {
                return ReleaseInternal(true);
            }
        }

        private void OnDisable()
        {
            using (LifecycleMarker.Auto())
            {
                ReleaseInternal(true);
            }
        }

        private bool ReleaseInternal(bool countRelease)
        {
            bool hadAcquisition = _isAcquired ||
                (caster != null && caster.HasRuntimeBinding);
            uint stableGroupId = _identity.StableGroupId;
            uint generation = _identity.Generation;

            if (binder != null)
                binder.ReleaseAcquisition(caster);
            else if (caster != null)
                caster.Unbind();

            _isAcquired = false;
            _identity = default;
            if (hadAcquisition && stableGroupId != 0u)
                CapsuleShadowCaster.ReleaseGroup(stableGroupId, generation);
            if (hadAcquisition && countRelease)
                ReleaseCount = Increment(ReleaseCount);
            return hadAcquisition;
        }

        private bool Reject(
            HeroRockCapsuleShadowAcquireFailure rejectedFailure,
            out HeroRockCapsuleShadowAcquireFailure failure)
        {
            LastFailure = rejectedFailure;
            RejectedAcquireCount = Increment(RejectedAcquireCount);
            failure = rejectedFailure;
            return false;
        }

        private static bool SameIdentity(
            in HeroRockCapsuleShadowIdentity left,
            in HeroRockCapsuleShadowIdentity right)
        {
            return left.ProducerKind == right.ProducerKind &&
                left.StableGroupId == right.StableGroupId &&
                left.Generation == right.Generation;
        }

        private static uint Increment(uint value)
        {
            return value == uint.MaxValue ? uint.MaxValue : value + 1u;
        }
    }
}
