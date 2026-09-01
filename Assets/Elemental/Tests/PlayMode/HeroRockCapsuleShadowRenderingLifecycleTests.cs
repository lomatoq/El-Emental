using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class HeroRockCapsuleShadowRenderingLifecycleTests
    {
        [UnityTest]
        public IEnumerator PoolLifecycleRequiresExplicitNewGenerationAndRejectsTinyProxy()
        {
            const uint groupId = 0xF3000001u;
            const uint staleGeneration = 0xE3000010u;
            const uint currentGeneration = 0xE3000011u;
            CapsuleShadowCasterBinder binder = CreateBinder();
            ProducerFixture fixture = CreateProducer("Pooled Hero Rock", binder, 0.1f);
            try
            {
                yield return null;
                Assert.That(fixture.Producer.IsAcquired, Is.False);
                Assert.That(fixture.Caster.IsRegistered, Is.False);

                HeroRockCapsuleShadowIdentity stale = Identity(
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    staleGeneration);
                Assert.That(fixture.Producer.TryAcquire(
                    stale,
                    Settings(),
                    out HeroRockCapsuleShadowAcquireFailure tinyFailure), Is.False);
                Assert.That(tinyFailure,
                    Is.EqualTo(HeroRockCapsuleShadowAcquireFailure.BelowMinimumDiameter));
                Assert.That(fixture.Caster.IsRegistered, Is.False);

                ConfigureProxy(fixture.Root.transform, fixture.Caster, 0.5f);
                Assert.That(fixture.Producer.TryAcquire(
                    stale,
                    Settings(),
                    out HeroRockCapsuleShadowAcquireFailure acquireFailure), Is.True);
                Assert.That(acquireFailure,
                    Is.EqualTo(HeroRockCapsuleShadowAcquireFailure.None));
                Assert.That(fixture.Caster.IsRegistered, Is.True);
                Assert.That(fixture.Producer.IsActiveGeneration, Is.False);
                Assert.That(binder.CommitGeneration(groupId, staleGeneration), Is.True);
                Assert.That(fixture.Producer.IsActiveGeneration, Is.True);
                Assert.That(fixture.Producer.TryAcquire(
                    stale,
                    Settings(),
                    out _), Is.True,
                    "A duplicate acquire for the same live epoch must be idempotent.");
                Assert.That(fixture.Producer.IsActiveGeneration, Is.True);
                Assert.That(fixture.Producer.ReleaseCount, Is.Zero);

                fixture.Root.SetActive(false);
                Assert.That(fixture.Producer.IsAcquired, Is.False);
                Assert.That(fixture.Caster.IsRegistered, Is.False);
                fixture.Root.SetActive(true);
                yield return null;
                Assert.That(fixture.Producer.IsAcquired, Is.False,
                    "Pool re-enable must not resurrect the previous acquisition.");
                Assert.That(fixture.Caster.IsRegistered, Is.False);

                Assert.That(fixture.Producer.TryAcquire(
                    stale,
                    Settings(),
                    out _), Is.True);
                Assert.That(fixture.Producer.IsActiveGeneration, Is.False);
                Assert.That(binder.CommitGeneration(groupId, staleGeneration), Is.False,
                    "A released pool epoch must remain a tombstone.");
                Assert.That(fixture.Producer.Release(), Is.True);

                HeroRockCapsuleShadowIdentity current = Identity(
                    CapsuleShadowProducerKind.IntactHeroRock,
                    groupId,
                    currentGeneration);
                Assert.That(fixture.Producer.TryAcquire(
                    current,
                    Settings(),
                    out _), Is.True);
                Assert.That(binder.CommitGeneration(groupId, currentGeneration), Is.True);
                Assert.That(fixture.Producer.IsActiveGeneration, Is.True);
                Assert.That(fixture.Producer.Release(), Is.True);
                Assert.That(fixture.Producer.Release(), Is.False);
                Assert.That(fixture.Producer.AcquireAttemptCount, Is.EqualTo(5u));
                Assert.That(fixture.Producer.RejectedAcquireCount, Is.EqualTo(1u));
                Assert.That(fixture.Producer.SuccessfulAcquireCount, Is.EqualTo(4u));
            }
            finally
            {
                fixture.Dispose();
                Object.Destroy(binder.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator IntactToLargeFragmentGenerationHandoffIsAtomic()
        {
            const uint groupId = 0xF3000002u;
            const uint intactGeneration = 0xE3000020u;
            const uint fractureGeneration = 0xE3000021u;
            CapsuleShadowCasterBinder binder = CreateBinder();
            ProducerFixture intact = CreateProducer("Intact Hero Rock", binder, 0.5f);
            ProducerFixture fragmentA = CreateProducer("Large Fragment A", binder, 0.5f);
            ProducerFixture fragmentB = CreateProducer("Large Fragment B", binder, 0.5f);
            try
            {
                Assert.That(intact.Producer.TryAcquire(
                    Identity(
                        CapsuleShadowProducerKind.IntactHeroRock,
                        groupId,
                        intactGeneration),
                    Settings(),
                    out _), Is.True);
                Assert.That(binder.CommitGeneration(groupId, intactGeneration), Is.True);
                Assert.That(intact.Producer.IsActiveGeneration, Is.True);

                HeroRockCapsuleShadowIdentity fractureIdentity = Identity(
                    CapsuleShadowProducerKind.LargeActiveFracture,
                    groupId,
                    fractureGeneration);
                Assert.That(fragmentA.Producer.TryAcquire(
                    fractureIdentity,
                    Settings(),
                    out _), Is.True);
                Assert.That(fragmentB.Producer.TryAcquire(
                    fractureIdentity,
                    Settings(),
                    out _), Is.True);
                Assert.That(intact.Producer.IsActiveGeneration, Is.True);
                Assert.That(fragmentA.Producer.IsActiveGeneration, Is.False);
                Assert.That(fragmentB.Producer.IsActiveGeneration, Is.False);
                Assert.That(CountActiveProxies(), Is.EqualTo(1));

                Assert.That(binder.CommitGeneration(groupId, fractureGeneration), Is.True);
                yield return null;
                Assert.That(intact.Producer.IsActiveGeneration, Is.False);
                Assert.That(fragmentA.Producer.IsActiveGeneration, Is.True);
                Assert.That(fragmentB.Producer.IsActiveGeneration, Is.True);
                Assert.That(CountActiveProxies(), Is.EqualTo(2));
            }
            finally
            {
                intact.Dispose();
                fragmentA.Dispose();
                fragmentB.Dispose();
                Object.Destroy(binder.gameObject);
            }
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

        private static HeroRockCapsuleShadowIdentity Identity(
            CapsuleShadowProducerKind producerKind,
            uint stableGroupId,
            uint generation)
        {
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                producerKind,
                stableGroupId,
                generation,
                out HeroRockCapsuleShadowIdentity identity), Is.True);
            return identity;
        }

        private static CapsuleShadowCasterBinder CreateBinder()
        {
            return new GameObject("Hero Rock Capsule Shadow Binder")
                .AddComponent<CapsuleShadowCasterBinder>();
        }

        private static ProducerFixture CreateProducer(
            string name,
            CapsuleShadowCasterBinder binder,
            float radius)
        {
            var root = new GameObject(name);
            CapsuleShadowCaster caster = root.AddComponent<CapsuleShadowCaster>();
            ConfigureProxy(root.transform, caster, radius);
            HeroRockCapsuleShadowProducer producer =
                root.AddComponent<HeroRockCapsuleShadowProducer>();
            Assert.That(producer.Configure(caster, binder), Is.True);
            return new ProducerFixture(root, caster, producer);
        }

        private static void ConfigureProxy(
            Transform root,
            CapsuleShadowCaster caster,
            float radius)
        {
            Assert.That(caster.ConfigureProxies(new[]
            {
                new CapsuleShadowProxyBinding(
                    root,
                    root,
                    Vector3.zero,
                    Vector3.zero,
                    radius,
                    0.08f)
            }), Is.True);
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
                0.4f,
                0.75f,
                CapsuleContactShadowDebugView.None);
        }

        private readonly struct ProducerFixture
        {
            public ProducerFixture(
                GameObject root,
                CapsuleShadowCaster caster,
                HeroRockCapsuleShadowProducer producer)
            {
                Root = root;
                Caster = caster;
                Producer = producer;
            }

            public GameObject Root { get; }
            public CapsuleShadowCaster Caster { get; }
            public HeroRockCapsuleShadowProducer Producer { get; }

            public void Dispose()
            {
                if (Producer != null)
                    Producer.Release();
                if (Root != null)
                    Object.Destroy(Root);
            }
        }
    }
}
