using System;
using System.Reflection;
using Elemental.Presentation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class HeroRockCapsuleShadowRenderingTests
    {
        [Test]
        public void TypedIdentityAdmitsOnlyHeroRocksAndLargeActiveFragments()
        {
            const uint highGroupId = 0xF1234567u;
            const uint highGeneration = 0xE2345678u;

            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.IntactHeroRock,
                highGroupId,
                highGeneration,
                out HeroRockCapsuleShadowIdentity rock), Is.True);
            Assert.That(rock.StableGroupId, Is.EqualTo(highGroupId));
            Assert.That(rock.Generation, Is.EqualTo(highGeneration));
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.LargeActiveFracture,
                highGroupId,
                highGeneration,
                out _), Is.True);
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.Debris,
                highGroupId,
                highGeneration,
                out _), Is.False);
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.Vfx,
                highGroupId,
                highGeneration,
                out _), Is.False);
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.Player,
                highGroupId,
                highGeneration,
                out _), Is.False);
            Assert.That(HeroRockCapsuleShadowIdentity.TryCreate(
                CapsuleShadowProducerKind.IntactHeroRock,
                0u,
                highGeneration,
                out _), Is.False);
            Assert.That(typeof(HeroRockCapsuleShadowIdentity).GetConstructors(), Is.Empty,
                "Raw public identity construction would bypass the typed producer policy.");
        }

        [Test]
        public void AdmissionRejectsTinyDebrisSizedProxiesAndNonFiniteDiameter()
        {
            CapsuleContactShadowRuntimeSettings settings = Settings();
            HeroRockCapsuleShadowIdentity rock = Identity(
                CapsuleShadowProducerKind.IntactHeroRock,
                0xF2000001u,
                1u);
            HeroRockCapsuleShadowIdentity fragment = Identity(
                CapsuleShadowProducerKind.LargeActiveFracture,
                0xF2000002u,
                1u);

            AssertAdmitted(rock, settings.MinimumHeroRockDiameter, settings,
                CapsuleShadowCasterClass.HeroRock);
            AssertRejected(
                rock,
                settings.MinimumHeroRockDiameter - 0.001f,
                settings,
                HeroRockCapsuleShadowAcquireFailure.BelowMinimumDiameter);
            AssertAdmitted(fragment, settings.MinimumActiveFragmentDiameter, settings,
                CapsuleShadowCasterClass.ActiveFragment);
            AssertRejected(
                fragment,
                settings.MinimumActiveFragmentDiameter - 0.001f,
                settings,
                HeroRockCapsuleShadowAcquireFailure.BelowMinimumDiameter);
            AssertRejected(
                fragment,
                float.NaN,
                settings,
                HeroRockCapsuleShadowAcquireFailure.InvalidDiameter);
            AssertRejected(
                fragment,
                float.PositiveInfinity,
                settings,
                HeroRockCapsuleShadowAcquireFailure.InvalidDiameter);

            var debrisIdentity = new HeroRockCapsuleShadowIdentity(
                CapsuleShadowProducerKind.Debris,
                0xF2000003u,
                1u);
            AssertRejected(
                debrisIdentity,
                10f,
                settings,
                HeroRockCapsuleShadowAcquireFailure.UnsupportedProducer);
        }

        [Test]
        public void AdmissionPolicyAllocatesNoManagedMemoryAfterWarmup()
        {
            CapsuleContactShadowRuntimeSettings settings = Settings();
            HeroRockCapsuleShadowIdentity identity = Identity(
                CapsuleShadowProducerKind.LargeActiveFracture,
                0xF2000011u,
                uint.MaxValue);
            HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                identity,
                1f,
                settings,
                out _,
                out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                if (!HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                        identity,
                        1f,
                        settings,
                        out CapsuleShadowCasterClass classification,
                        out HeroRockCapsuleShadowAcquireFailure failure) ||
                    classification != CapsuleShadowCasterClass.ActiveFragment ||
                    failure != HeroRockCapsuleShadowAcquireFailure.None)
                    Assert.Fail("Hero-rock admission changed during the allocation window.");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void RuntimeProducerHasNoEnableAcquisitionOrRawUintAcquireSeam()
        {
            Assert.That(typeof(HeroRockCapsuleShadowProducer).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            MethodInfo[] methods = typeof(HeroRockCapsuleShadowProducer).GetMethods(
                BindingFlags.Instance | BindingFlags.Public);
            int acquireMethodCount = 0;
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                if (!string.Equals(methods[methodIndex].Name, "TryAcquire",
                        StringComparison.Ordinal))
                    continue;
                acquireMethodCount++;
                ParameterInfo[] parameters = methods[methodIndex].GetParameters();
                Assert.That(Array.Exists(
                    parameters,
                    parameter => parameter.ParameterType == typeof(uint)), Is.False,
                    "The pool adapter must consume the typed identity, not raw uint pairs.");
                Assert.That(Array.Exists(
                    parameters,
                    parameter => parameter.ParameterType ==
                        typeof(HeroRockCapsuleShadowIdentity).MakeByRefType()), Is.True);
            }
            Assert.That(acquireMethodCount, Is.EqualTo(1));
        }

        private static void AssertAdmitted(
            in HeroRockCapsuleShadowIdentity identity,
            float diameter,
            in CapsuleContactShadowRuntimeSettings settings,
            CapsuleShadowCasterClass expectedClassification)
        {
            Assert.That(HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                identity,
                diameter,
                settings,
                out CapsuleShadowCasterClass classification,
                out HeroRockCapsuleShadowAcquireFailure failure), Is.True);
            Assert.That(classification, Is.EqualTo(expectedClassification));
            Assert.That(failure, Is.EqualTo(HeroRockCapsuleShadowAcquireFailure.None));
        }

        private static void AssertRejected(
            in HeroRockCapsuleShadowIdentity identity,
            float diameter,
            in CapsuleContactShadowRuntimeSettings settings,
            HeroRockCapsuleShadowAcquireFailure expectedFailure)
        {
            Assert.That(HeroRockCapsuleShadowProducerPolicy.TryAdmit(
                identity,
                diameter,
                settings,
                out _,
                out HeroRockCapsuleShadowAcquireFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
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

        private static CapsuleContactShadowRuntimeSettings Settings()
        {
            return new CapsuleContactShadowRuntimeSettings(
                new CapsuleContactShadowQuality(20),
                12,
                0.58f,
                1.25f,
                0.025f,
                0.02f,
                0.4f,
                0.75f,
                CapsuleContactShadowDebugView.None);
        }
    }
}
