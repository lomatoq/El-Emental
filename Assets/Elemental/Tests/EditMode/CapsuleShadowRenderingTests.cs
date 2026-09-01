using System;
using System.IO;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class CapsuleShadowRenderingTests
    {
        private const string ProfilePath =
            "Assets/Elemental/Content/GraphicsVNext/Rendering/DuelRenderingProfile.asset";

        [Test]
        public void AnalyticReferenceIsFiniteContactBoundedAndBiasSafe()
        {
            var near = new CapsuleShadowProxy(
                new Vector3(0f, 0.28f, 0f),
                new Vector3(0f, 0.58f, 0f),
                0.12f,
                0.04f);
            var beyondContact = new CapsuleShadowProxy(
                new Vector3(0f, 2.5f, 0f),
                new Vector3(0f, 2.8f, 0f),
                0.12f,
                0.04f);

            float nearAttenuation = CapsuleContactShadowMath.Evaluate(
                near,
                Vector3.zero,
                Vector3.up,
                Vector3.up,
                1.25f,
                0.025f,
                0.02f,
                0.6f);
            float farAttenuation = CapsuleContactShadowMath.Evaluate(
                beyondContact,
                Vector3.zero,
                Vector3.up,
                Vector3.up,
                1.25f,
                0.025f,
                0.02f,
                0.6f);
            float invalidAttenuation = CapsuleContactShadowMath.Evaluate(
                near,
                Vector3.zero,
                Vector3.zero,
                Vector3.up,
                1.25f,
                0.025f,
                0.02f,
                0.6f);

            Assert.That(float.IsNaN(nearAttenuation), Is.False);
            Assert.That(nearAttenuation, Is.InRange(0.4f, 0.999f));
            Assert.That(farAttenuation, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(invalidAttenuation, Is.EqualTo(1f));
        }

        [Test]
        public void BufferUsesCanonicalUintAndRejectsStaleGenerationAcrossEmptyInterval()
        {
            const uint groupId = 0xF1234567u;
            const uint staleGeneration = 0xE2345677u;
            const uint currentGeneration = 0xE2345678u;
            CasterFixture stale = CreateCaster("Stale", Vector3.zero);
            CasterFixture current = CreateCaster("Current", Vector3.right);
            var buffer = new CapsuleShadowBuffer(4, 2);
            try
            {
                Assert.That(buffer.TryRegister(
                    Record(stale.Caster, groupId, staleGeneration),
                    out CapsuleShadowRegistrationHandle staleHandle), Is.True);
                Assert.That(buffer.TryRegister(
                    Record(current.Caster, groupId, currentGeneration),
                    out CapsuleShadowRegistrationHandle currentHandle), Is.True);
                Assert.That(buffer.TryCommitGeneration(groupId, currentGeneration), Is.True);
                Assert.That(buffer.IsGenerationActive(staleHandle), Is.False);
                Assert.That(buffer.IsGenerationActive(currentHandle), Is.True);
                Assert.That(buffer.Unregister(staleHandle), Is.True);
                Assert.That(buffer.Unregister(currentHandle), Is.True);

                Assert.That(buffer.TryRegister(
                    Record(stale.Caster, groupId, staleGeneration),
                    out CapsuleShadowRegistrationHandle reacquiredStale), Is.True);
                Assert.That(buffer.IsGenerationActive(reacquiredStale), Is.False);
                Assert.That(buffer.TryRegister(
                    Record(current.Caster, groupId, currentGeneration),
                    out CapsuleShadowRegistrationHandle reacquiredCurrent), Is.True);
                Assert.That(buffer.IsGenerationActive(reacquiredCurrent), Is.True);
            }
            finally
            {
                stale.Dispose();
                current.Dispose();
            }
        }

        [Test]
        public void CopyIsDeterministicBoundedAndAllocationFreeAfterWarmup()
        {
            CasterFixture hero = CreateCaster("Hero", new Vector3(2f, 0f, 0f));
            CasterFixture character = CreateCaster("Character", new Vector3(1f, 0f, 0f));
            var buffer = new CapsuleShadowBuffer(4, 4);
            var startRadius = new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
            var endSoftness = new Vector4[CapsuleShadowBuffer.MaximumProxyCount];
            CapsuleContactShadowRuntimeSettings settings = Settings(2);
            try
            {
                Assert.That(buffer.TryRegister(
                    Record(hero.Caster, 8u, 0u, CapsuleShadowCasterClass.HeroRock),
                    out _), Is.True);
                Assert.That(buffer.TryCommitGeneration(8u, 0u), Is.True);
                Assert.That(buffer.TryRegister(
                    Record(character.Caster, 9u, 0u, CapsuleShadowCasterClass.Character),
                    out _), Is.True);
                Assert.That(buffer.TryCommitGeneration(9u, 0u), Is.True);
                Assert.That(buffer.CopyActiveProxies(
                    startRadius,
                    endSoftness,
                    settings,
                    out int activeCasters,
                    out _,
                    out _), Is.EqualTo(2));
                Assert.That(activeCasters, Is.EqualTo(2));
                Assert.That(startRadius[0].x, Is.EqualTo(1f).Within(0.0001f),
                    "Character priority must precede hero-rock registration order.");

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < 128; index++)
                {
                    buffer.CopyActiveProxies(
                        startRadius,
                        endSoftness,
                        settings,
                        out _,
                        out _,
                        out _);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                hero.Dispose();
                character.Dispose();
            }
        }

        [Test]
        public void ShippingProfileIsOffAndOwnedShadersExposeBoundedDebugReceiver()
        {
            DuelRenderingProfile profile =
                AssetDatabase.LoadAssetAtPath<DuelRenderingProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.UseCapsuleContactShadows, Is.False);
            Assert.That(profile.CapsuleContactShadows.CreateRuntimeSettings()
                .Quality.MaximumCapsuleCount, Is.EqualTo(20));

            string renderingPath = Path.Combine(
                Application.dataPath,
                "Elemental/Content/GraphicsVNext/Rendering");
            string include = File.ReadAllText(Path.Combine(
                renderingPath,
                "CapsuleContactShadow.hlsl"));
            string debugShader = File.ReadAllText(Path.Combine(
                renderingPath,
                "CapsuleContactShadowDebug.shader"));
            string rendererAsset = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Settings/ElEmentalRenderer.asset"));
            StringAssert.Contains("ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES 32", include);
            StringAssert.Contains("proxyIndex < ELEMENTAL_MAX_CAPSULE_SHADOW_PROXIES", include);
            StringAssert.Contains("ElementalSampleCapsuleContactShadow", debugShader);
            StringAssert.Contains("CapsuleContactShadowFeature", rendererAsset);
        }

        [Test]
        public void CaptureOverrideIsSingleOwnerAndRestoresShippingState()
        {
            CapsuleContactShadowCaptureOverride.Token token = default;
            try
            {
                CapsuleContactShadowRuntimeSettings settings = Settings(2);
                Assert.That(CapsuleContactShadowCaptureOverride.TryBegin(
                    settings,
                    out token,
                    out string failure), Is.True, failure);
                Assert.That(CapsuleContactShadowCaptureOverride.IsActive, Is.True);
                Assert.That(CapsuleContactShadowCaptureOverride.TryBegin(
                    settings,
                    out _,
                    out string duplicateFailure), Is.False);
                StringAssert.Contains("already owns", duplicateFailure);
            }
            finally
            {
                token.Dispose();
            }
            Assert.That(CapsuleContactShadowCaptureOverride.IsActive, Is.False);
        }

        [Test]
        public void NewAndReleasedGroupsStayInactiveUntilExplicitCommit()
        {
            const uint groupId = 0xF9000001u;
            const uint firstGeneration = 0xE9000001u;
            const uint nextGeneration = 0xE9000002u;
            CasterFixture fixture = CreateCaster("Explicit Commit", Vector3.zero);
            var buffer = new CapsuleShadowBuffer(2, 2);
            try
            {
                Assert.That(buffer.TryRegister(
                    Record(fixture.Caster, groupId, firstGeneration),
                    out CapsuleShadowRegistrationHandle first), Is.True);
                Assert.That(buffer.IsGenerationActive(first), Is.False);
                Assert.That(buffer.TryCommitGeneration(groupId, firstGeneration), Is.True);
                Assert.That(buffer.IsGenerationActive(first), Is.True);
                Assert.That(buffer.Unregister(first), Is.True);
                Assert.That(buffer.TryReleaseGroup(groupId, firstGeneration), Is.True);

                Assert.That(buffer.TryRegister(
                    Record(fixture.Caster, groupId, firstGeneration),
                    out CapsuleShadowRegistrationHandle stale), Is.True);
                Assert.That(buffer.IsGenerationActive(stale), Is.False,
                    "A stale pooled bind after group release must not resurrect itself.");
                Assert.That(buffer.TryCommitGeneration(groupId, firstGeneration), Is.False,
                    "A released epoch must remain a tombstone for explicit stale commits.");
                Assert.That(buffer.Unregister(stale), Is.True);
                Assert.That(buffer.TryRegister(
                    Record(fixture.Caster, groupId, nextGeneration),
                    out CapsuleShadowRegistrationHandle next), Is.True);
                Assert.That(buffer.IsGenerationActive(next), Is.False);
                Assert.That(buffer.TryCommitGeneration(groupId, nextGeneration), Is.True);
                Assert.That(buffer.IsGenerationActive(next), Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OwnershipClassificationIsTypedAndFailsClosed()
        {
            AssertIdentity(CapsuleShadowProducerKind.Player, CapsuleShadowCasterClass.Character);
            AssertIdentity(CapsuleShadowProducerKind.OpponentBot, CapsuleShadowCasterClass.Character);
            AssertIdentity(CapsuleShadowProducerKind.Ragdoll, CapsuleShadowCasterClass.Character);
            AssertIdentity(CapsuleShadowProducerKind.IntactHeroRock, CapsuleShadowCasterClass.HeroRock);
            AssertIdentity(
                CapsuleShadowProducerKind.LargeActiveFracture,
                CapsuleShadowCasterClass.ActiveFragment);
            Assert.That(CapsuleShadowOwnershipPolicy.TryCreateIdentity(
                CapsuleShadowProducerKind.Debris, 91u, 7u, out _), Is.False);
            Assert.That(CapsuleShadowOwnershipPolicy.TryCreateIdentity(
                CapsuleShadowProducerKind.Vfx, 91u, 7u, out _), Is.False);
            Assert.That(CapsuleShadowOwnershipPolicy.TryCreateIdentity(
                CapsuleShadowProducerKind.Player, 0u, 7u, out _), Is.False);
            Assert.That(new CapsuleShadowCasterIdentity(
                91u, 7u, CapsuleShadowCasterClass.TinyDebris).IsValid, Is.False);
            Assert.That(new CapsuleShadowCasterIdentity(
                91u, 7u, CapsuleShadowCasterClass.Vfx).IsValid, Is.False);
        }

        private static void AssertIdentity(
            CapsuleShadowProducerKind producer,
            CapsuleShadowCasterClass expectedClass)
        {
            Assert.That(CapsuleShadowOwnershipPolicy.TryCreateIdentity(
                producer, 0xF0000091u, 0xE0000007u,
                out CapsuleShadowCasterIdentity identity), Is.True);
            Assert.That(identity.IsValid, Is.True);
            Assert.That(identity.StableGroupId, Is.EqualTo(0xF0000091u));
            Assert.That(identity.Generation, Is.EqualTo(0xE0000007u));
            Assert.That(identity.Classification, Is.EqualTo(expectedClass));
        }

        private static CapsuleContactShadowRuntimeSettings Settings(int maximumCasters)
        {
            return new CapsuleContactShadowRuntimeSettings(
                new CapsuleContactShadowQuality(20),
                maximumCasters,
                0.58f,
                1.25f,
                0.025f,
                0.02f,
                0.4f,
                0.75f,
                CapsuleContactShadowDebugView.None);
        }

        private static CapsuleShadowCasterRecord Record(
            CapsuleShadowCaster caster,
            uint groupId,
            uint generation,
            CapsuleShadowCasterClass classification = CapsuleShadowCasterClass.ActiveFragment)
        {
            return new CapsuleShadowCasterRecord(
                caster,
                groupId,
                generation,
                classification);
        }

        private static CasterFixture CreateCaster(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
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

        private readonly struct CasterFixture : IDisposable
        {
            public CasterFixture(GameObject root, CapsuleShadowCaster caster)
            {
                Root = root;
                Caster = caster;
            }

            public GameObject Root { get; }
            public CapsuleShadowCaster Caster { get; }

            public void Dispose()
            {
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
