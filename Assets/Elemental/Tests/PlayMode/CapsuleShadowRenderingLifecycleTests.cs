using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class CapsuleShadowRenderingLifecycleTests
    {
        [UnityTest]
        public IEnumerator BindDisableAndReacquireRequireCurrentExplicitIdentity()
        {
            const uint groupId = 0xF1000041u;
            const uint generation = 0xE2000002u;
            CasterFixture fixture = CreateCaster("Capsule Pooled Character");
            CapsuleShadowCasterBinder binder = CreateBinder();
            try
            {
                yield return null;
                Assert.That(fixture.Caster.HasValidBinding, Is.False);
                Assert.That(fixture.Caster.IsRegistered, Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    default,
                    groupId,
                    generation), Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    (CapsuleShadowProducerKind)255,
                    groupId,
                    generation), Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    0u,
                    generation), Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Debris,
                    groupId,
                    generation), Is.False);
                Assert.That(fixture.Caster.HasValidBinding, Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    groupId,
                    generation), Is.True);
                Assert.That(fixture.Caster.IsRegistered, Is.True);
                Assert.That(fixture.Caster.IsActiveGeneration, Is.False);
                Assert.That(binder.CommitGeneration(groupId, generation), Is.True);
                Assert.That(fixture.Caster.IsActiveGeneration, Is.True);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Vfx,
                    groupId,
                    generation), Is.False);
                Assert.That(fixture.Caster.IsRegistered, Is.False,
                    "A rejected typed reacquisition must clear the prior pooled handle.");
                Assert.That(fixture.Caster.HasRuntimeBinding, Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    groupId,
                    generation), Is.True);
                binder.ReleaseAcquisition(fixture.Caster);
                Assert.That(fixture.Caster.IsRegistered, Is.False);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    groupId,
                    generation), Is.True);
                Assert.That(fixture.Caster.IsActiveGeneration, Is.True);

                fixture.Root.SetActive(false);
                Assert.That(fixture.Caster.IsRegistered, Is.False);
                Assert.That(fixture.Caster.HasRuntimeBinding, Is.False);
                fixture.Root.SetActive(true);
                yield return null;
                Assert.That(fixture.Caster.IsRegistered, Is.False,
                    "Pool re-enable must not restore the previous acquisition.");
                Assert.That(fixture.Caster.HasValidBinding, Is.False,
                    "No serialized or cached identity may survive OnDisable.");
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    groupId,
                    generation), Is.True);
                Assert.That(fixture.Caster.IsRegistered, Is.True);
            }
            finally
            {
                Cleanup(fixture, binder, groupId, generation);
            }
        }

        [UnityTest]
        public IEnumerator StaleGenerationStaysInactiveAfterEmptyPoolInterval()
        {
            const uint groupId = 0xF1000051u;
            const uint staleGeneration = 0xE2000010u;
            const uint currentGeneration = 0xE2000011u;
            CasterFixture stale = CreateCaster("Capsule Stale");
            CasterFixture current = CreateCaster("Capsule Current");
            CapsuleShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(binder.TryAcquire(
                    stale.Caster,
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    staleGeneration), Is.True);
                Assert.That(binder.TryAcquire(
                    current.Caster,
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    currentGeneration), Is.True);
                Assert.That(binder.CommitGeneration(groupId, currentGeneration), Is.True);
                binder.ReleaseAcquisition(stale.Caster);
                binder.ReleaseAcquisition(current.Caster);
                yield return null;

                Assert.That(binder.TryAcquire(
                    stale.Caster,
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    staleGeneration), Is.True);
                Assert.That(stale.Caster.IsActiveGeneration, Is.False);
                Assert.That(binder.TryAcquire(
                    current.Caster,
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    currentGeneration), Is.True);
                Assert.That(current.Caster.IsActiveGeneration, Is.True);
            }
            finally
            {
                Cleanup(stale, null, 0u, 0u);
                Cleanup(current, binder, groupId, currentGeneration);
            }
        }

        [UnityTest]
        public IEnumerator AtomicGenerationHandoffSwitchesCompleteProxySetTogether()
        {
            const uint groupId = 0xF1000061u;
            const uint intactGeneration = 0xFFFFFFFEu;
            const uint fractureGeneration = uint.MaxValue;
            CasterFixture intact = CreateCaster("Capsule Intact");
            CasterFixture fragmentA = CreateCaster("Capsule Fragment A");
            CasterFixture fragmentB = CreateCaster("Capsule Fragment B");
            CapsuleShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(binder.TryAcquire(
                    intact.Caster,
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    intactGeneration), Is.True);
                Assert.That(intact.Caster.IsActiveGeneration, Is.False);
                Assert.That(binder.CommitGeneration(groupId, intactGeneration), Is.True);
                Assert.That(binder.TryAcquire(
                    fragmentA.Caster,
                    CapsuleShadowProducerKind.LargeActiveFracture,
                    groupId,
                    fractureGeneration), Is.True);
                Assert.That(binder.TryAcquire(
                    fragmentB.Caster,
                    CapsuleShadowProducerKind.LargeActiveFracture,
                    groupId,
                    fractureGeneration), Is.True);
                Assert.That(intact.Caster.IsActiveGeneration, Is.True);
                Assert.That(fragmentA.Caster.IsActiveGeneration, Is.False);
                Assert.That(fragmentB.Caster.IsActiveGeneration, Is.False);

                Assert.That(binder.CommitGeneration(groupId, fractureGeneration), Is.True);
                yield return null;
                Assert.That(intact.Caster.IsActiveGeneration, Is.False);
                Assert.That(fragmentA.Caster.IsActiveGeneration, Is.True);
                Assert.That(fragmentB.Caster.IsActiveGeneration, Is.True);
                Assert.That(CountActiveProxies(), Is.EqualTo(2));
            }
            finally
            {
                Cleanup(intact, null, 0u, 0u);
                Cleanup(fragmentA, null, 0u, 0u);
                Cleanup(fragmentB, binder, groupId, fractureGeneration);
            }
        }

        [UnityTest]
        public IEnumerator ActiveEligibilityTracksCasterComponentAndRebindIsIdempotent()
        {
            const uint oldGroup = 0xF1000071u;
            const uint nextGroup = 0xF1000072u;
            const uint generation = 7u;
            CasterFixture fixture = CreateCaster("Capsule Eligibility");
            CapsuleShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    oldGroup,
                    generation), Is.True);
                Assert.That(binder.CommitGeneration(oldGroup, generation), Is.True);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    nextGroup,
                    generation), Is.True);
                Assert.That(binder.CommitGeneration(nextGroup, generation), Is.True);
                Assert.That(binder.TryAcquire(
                    fixture.Caster,
                    CapsuleShadowProducerKind.Player,
                    nextGroup,
                    generation), Is.True);
                Assert.That(CapsuleShadowBuffer.Shared.Count, Is.GreaterThanOrEqualTo(1));
                Assert.That(CountActiveProxies(), Is.EqualTo(1));
                Assert.That(binder.ReleaseGroup(oldGroup, generation), Is.True);

                fixture.Caster.enabled = false;
                yield return null;
                Assert.That(fixture.Caster.IsRegistered, Is.False);
                Assert.That(CountActiveProxies(), Is.Zero);
            }
            finally
            {
                Cleanup(fixture, binder, nextGroup, generation);
            }
        }

        [UnityTest]
        public IEnumerator FeatureAndCaptureTeardownClearGlobalsWithoutAnotherCameraFrame()
        {
            CapsuleContactShadowFeature feature =
                ScriptableObject.CreateInstance<CapsuleContactShadowFeature>();
            CapsuleContactShadowCaptureOverride.Token token = default;
            try
            {
                SetNonzeroGlobals();
                feature.Create();
                AssertGlobalsCleared();

                Assert.That(CapsuleContactShadowCaptureOverride.TryBegin(
                    Settings(), out token, out string failure), Is.True, failure);
                SetNonzeroGlobals();
                token.Dispose();
                token = default;
                AssertGlobalsCleared();

                SetNonzeroGlobals();
                Object.DestroyImmediate(feature);
                feature = null;
                AssertGlobalsCleared();
                Assert.That(CapsuleContactShadowDiagnostics.Current.ShadowStrength, Is.Zero);
            }
            finally
            {
                token.Dispose();
                if (feature != null)
                    Object.DestroyImmediate(feature);
                CapsuleContactShadowFeature.ClearGlobalState();
            }
            yield return null;
        }

        private static void SetNonzeroGlobals()
        {
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.ShadowParamsId,
                new Vector4(1f, 0.7f, 1.25f, 3f));
            Shader.SetGlobalVector(
                CapsuleContactShadowRenderPass.BiasDebugParamsId,
                new Vector4(0.02f, 0.03f, 1f, 0f));
        }

        private static void AssertGlobalsCleared()
        {
            Assert.That(Shader.GetGlobalVector(
                CapsuleContactShadowRenderPass.ShadowParamsId), Is.EqualTo(Vector4.zero));
            Assert.That(Shader.GetGlobalVector(
                CapsuleContactShadowRenderPass.BiasDebugParamsId), Is.EqualTo(Vector4.zero));
        }

        private static int CountActiveProxies()
        {
            var startRadius = new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
            var endSoftness = new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
            return CapsuleShadowBuffer.Shared.CopyActiveProxies(
                startRadius,
                endSoftness,
                Settings(),
                out _,
                out _,
                out _);
        }

        private static CapsuleContactShadowRuntimeSettings Settings()
        {
            return new CapsuleContactShadowRuntimeSettings(
                new CapsuleContactShadowQuality(32),
                CapsuleShadowBuffer.MaximumCasterCount,
                0.58f,
                1.25f,
                0.025f,
                0.02f,
                0.1f,
                0.1f,
                CapsuleContactShadowDebugView.None);
        }

        private static CapsuleShadowCasterBinder CreateBinder()
        {
            return new GameObject("Capsule Shadow Binder")
                .AddComponent<CapsuleShadowCasterBinder>();
        }

        private static CasterFixture CreateCaster(string name)
        {
            var root = new GameObject(name);
            CapsuleShadowCaster caster = root.AddComponent<CapsuleShadowCaster>();
            Assert.That(caster.ConfigureProxies(new[]
            {
                new CapsuleShadowProxyBinding(
                    root.transform,
                    root.transform,
                    Vector3.zero,
                    Vector3.zero,
                    0.5f,
                    0.08f)
            }), Is.True);
            return new CasterFixture(root, caster);
        }

        private static void Cleanup(
            CasterFixture fixture,
            CapsuleShadowCasterBinder binder,
            uint groupId,
            uint generation)
        {
            if (fixture.Caster != null)
                fixture.Caster.Unbind();
            if (binder != null && groupId != 0u)
                binder.ReleaseGroup(groupId, generation);
            if (fixture.Root != null)
                Object.Destroy(fixture.Root);
            if (binder != null)
                Object.Destroy(binder.gameObject);
        }

        private readonly struct CasterFixture
        {
            public CasterFixture(GameObject root, CapsuleShadowCaster caster)
            {
                Root = root;
                Caster = caster;
            }

            public GameObject Root { get; }
            public CapsuleShadowCaster Caster { get; }
        }
    }
}
