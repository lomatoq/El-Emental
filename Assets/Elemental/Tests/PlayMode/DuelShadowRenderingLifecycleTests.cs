using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class DuelShadowRenderingLifecycleTests
    {
        private static readonly DuelShadowClassificationSettings Classification =
            new DuelShadowClassificationSettings(0.45f, 0.8f);

        [UnityTest]
        public IEnumerator BindUsesCanonicalHighBitIdentityWithoutDefaultRegistration()
        {
            const uint groupId = 0xF1234567u;
            const uint generation = 0xE2345678u;
            CasterFixture fixture = CreateCaster("High Bit Caster");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                yield return null;
                Assert.That(fixture.Caster.HasValidBinding, Is.False);
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.Zero);

                var identity = new DuelShadowCasterIdentity(
                    groupId,
                    generation,
                    DuelShadowCasterClass.ActiveFragment);
                Assert.That(binder.Bind(fixture.Caster, in identity), Is.True);
                yield return null;

                Assert.That(fixture.Caster.StableGroupId, Is.EqualTo(groupId));
                Assert.That(fixture.Caster.Generation, Is.EqualTo(generation));
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.EqualTo(1));
                Assert.That(fixture.Caster.ActiveRegistrationCount, Is.EqualTo(1));
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    groupId,
                    generation), Is.True);
            }
            finally
            {
                Cleanup(fixture, binder, groupId, generation);
            }
        }

        [UnityTest]
        public IEnumerator DisableReenableRequiresExplicitRuntimeRebind()
        {
            const uint groupId = 0x80000011u;
            const uint generation = 3u;
            CasterFixture fixture = CreateCaster("Pooled Caster");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                var identity = new DuelShadowCasterIdentity(
                    groupId,
                    generation,
                    DuelShadowCasterClass.ActiveFragment);
                Assert.That(binder.Bind(fixture.Caster, in identity), Is.True);
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.EqualTo(1));

                fixture.Root.SetActive(false);
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.Zero);
                Assert.That(fixture.Caster.HasRuntimeBinding, Is.False);

                fixture.Root.SetActive(true);
                yield return null;
                Assert.That(fixture.Caster.HasValidBinding, Is.False);
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.Zero,
                    "OnEnable must not register the previous pooled acquisition.");

                Assert.That(binder.Bind(fixture.Caster, in identity), Is.True);
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.EqualTo(1));
            }
            finally
            {
                Cleanup(fixture, binder, groupId, generation);
            }
        }

        [UnityTest]
        public IEnumerator ReacquireRebindReplacesOldHandleIdempotently()
        {
            const uint oldGroup = 0x80000021u;
            const uint oldGeneration = 0xF0000001u;
            const uint currentGroup = 0x80000022u;
            const uint currentGeneration = 0xF0000002u;
            CasterFixture fixture = CreateCaster("Rebound Caster");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                var oldIdentity = new DuelShadowCasterIdentity(
                    oldGroup,
                    oldGeneration,
                    DuelShadowCasterClass.ActiveFragment);
                var currentIdentity = new DuelShadowCasterIdentity(
                    currentGroup,
                    currentGeneration,
                    DuelShadowCasterClass.HeroRock);
                Assert.That(binder.Bind(fixture.Caster, in oldIdentity), Is.True);
                Assert.That(binder.Bind(fixture.Caster, in currentIdentity), Is.True);
                Assert.That(binder.Bind(fixture.Caster, in currentIdentity), Is.True);
                yield return null;

                Assert.That(fixture.Caster.RegisteredRendererCount, Is.EqualTo(1));
                Assert.That(fixture.Caster.StableGroupId, Is.EqualTo(currentGroup));
                Assert.That(fixture.Caster.Generation, Is.EqualTo(currentGeneration));
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    oldGroup,
                    oldGeneration), Is.False);
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    currentGroup,
                    currentGeneration), Is.True);
                Assert.That(binder.ReleaseGroup(oldGroup, oldGeneration), Is.True,
                    "The old group must have no stale handle after rebind.");
            }
            finally
            {
                Cleanup(fixture, binder, currentGroup, currentGeneration);
            }
        }

        [UnityTest]
        public IEnumerator StaleGenerationCannotReactivateAfterPoolReenable()
        {
            const uint groupId = 0x80000031u;
            const uint staleGeneration = 0x80000001u;
            const uint currentGeneration = 0x80000002u;
            CasterFixture stale = CreateCaster("Stale Generation");
            CasterFixture current = CreateCaster("Current Generation");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(Bind(
                    binder,
                    stale.Caster,
                    groupId,
                    staleGeneration), Is.True);
                Assert.That(Bind(
                    binder,
                    current.Caster,
                    groupId,
                    currentGeneration), Is.True);
                Assert.That(binder.CommitGeneration(groupId, currentGeneration), Is.True);
                Assert.That(stale.Caster.ActiveRegistrationCount, Is.Zero);
                Assert.That(current.Caster.ActiveRegistrationCount, Is.EqualTo(1));

                stale.Root.SetActive(false);
                stale.Root.SetActive(true);
                yield return null;
                Assert.That(stale.Caster.RegisteredRendererCount, Is.Zero);
                Assert.That(Bind(
                    binder,
                    stale.Caster,
                    groupId,
                    staleGeneration), Is.True);
                Assert.That(stale.Caster.ActiveRegistrationCount, Is.Zero);
                Assert.That(current.Caster.ActiveRegistrationCount, Is.EqualTo(1));
            }
            finally
            {
                Cleanup(stale, null, 0u, 0u);
                Cleanup(current, binder, groupId, currentGeneration);
            }
        }

        [UnityTest]
        public IEnumerator RendererEligibilityTracksActiveRendererState()
        {
            const uint groupId = 0x80000041u;
            const uint generation = 5u;
            CasterFixture fixture = CreateCaster("Eligibility Caster");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(Bind(
                    binder,
                    fixture.Caster,
                    groupId,
                    generation), Is.True);
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    groupId,
                    generation), Is.True);

                fixture.Renderer.enabled = false;
                yield return null;
                Assert.That(fixture.Caster.RegisteredRendererCount, Is.EqualTo(1));
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    groupId,
                    generation), Is.False);

                fixture.Renderer.enabled = true;
                yield return null;
                Assert.That(ContainsDrawCommand(
                    fixture.Renderer,
                    groupId,
                    generation), Is.True);
            }
            finally
            {
                Cleanup(fixture, binder, groupId, generation);
            }
        }

        [UnityTest]
        public IEnumerator AtomicGenerationHandoffActivatesAllNewCastersTogether()
        {
            const uint groupId = 0x80000051u;
            const uint intactGeneration = 0xFFFFFFFEu;
            const uint fractureGeneration = uint.MaxValue;
            CasterFixture intact = CreateCaster("Intact Representation");
            CasterFixture fragmentA = CreateCaster("Fracture A");
            CasterFixture fragmentB = CreateCaster("Fracture B");
            DuelShadowCasterBinder binder = CreateBinder();
            try
            {
                Assert.That(Bind(
                    binder,
                    intact.Caster,
                    groupId,
                    intactGeneration), Is.True);
                Assert.That(Bind(
                    binder,
                    fragmentA.Caster,
                    groupId,
                    fractureGeneration), Is.True);
                Assert.That(Bind(
                    binder,
                    fragmentB.Caster,
                    groupId,
                    fractureGeneration), Is.True);
                Assert.That(intact.Caster.ActiveRegistrationCount, Is.EqualTo(1));
                Assert.That(fragmentA.Caster.ActiveRegistrationCount, Is.Zero);
                Assert.That(fragmentB.Caster.ActiveRegistrationCount, Is.Zero);

                Assert.That(binder.CommitGeneration(groupId, fractureGeneration), Is.True);
                yield return null;
                Assert.That(intact.Caster.ActiveRegistrationCount, Is.Zero);
                Assert.That(fragmentA.Caster.ActiveRegistrationCount, Is.EqualTo(1));
                Assert.That(fragmentB.Caster.ActiveRegistrationCount, Is.EqualTo(1));

                intact.Root.SetActive(false);
                yield return null;
                Assert.That(ContainsDrawCommand(
                    fragmentA.Renderer,
                    groupId,
                    fractureGeneration), Is.True);
                Assert.That(ContainsDrawCommand(
                    fragmentB.Renderer,
                    groupId,
                    fractureGeneration), Is.True);
            }
            finally
            {
                Cleanup(intact, null, 0u, 0u);
                Cleanup(fragmentA, null, 0u, 0u);
                Cleanup(fragmentB, binder, groupId, fractureGeneration);
            }
        }

        private static bool Bind(
            DuelShadowCasterBinder binder,
            DuelShadowCaster caster,
            uint groupId,
            uint generation)
        {
            var identity = new DuelShadowCasterIdentity(
                groupId,
                generation,
                DuelShadowCasterClass.ActiveFragment);
            return binder.Bind(caster, in identity);
        }

        private static bool ContainsDrawCommand(
            Renderer renderer,
            uint groupId,
            uint generation)
        {
            var commands = new DuelShadowDrawCommand[
                DuelShadowCasterRegistry.MaximumCapacity];
            int count = DuelShadowCasterRegistry.Shared.CopyActiveDrawCommands(
                commands,
                Classification,
                commands.Length,
                out _,
                out _);
            for (int index = 0; index < count; index++)
            {
                DuelShadowDrawCommand command = commands[index];
                if (command.Renderer == renderer &&
                    command.StableGroupId == groupId &&
                    command.Generation == generation)
                    return true;
            }
            return false;
        }

        private static DuelShadowCasterBinder CreateBinder()
        {
            return new GameObject("Duel Shadow Binder")
                .AddComponent<DuelShadowCasterBinder>();
        }

        private static CasterFixture CreateCaster(string name)
        {
            var root = new GameObject(name);
            Mesh mesh = new Mesh
            {
                name = $"{name} Mesh",
                vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(1f, -1f, 0f),
                    new Vector3(0f, 1f, 0f)
                },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            DuelShadowCaster caster = root.AddComponent<DuelShadowCaster>();
            return new CasterFixture(root, mesh, renderer, caster);
        }

        private static void Cleanup(
            CasterFixture fixture,
            DuelShadowCasterBinder binder,
            uint groupId,
            uint generation)
        {
            if (fixture.Caster != null)
                fixture.Caster.Unbind();
            if (binder != null && groupId != 0u)
                binder.ReleaseGroup(groupId, generation);
            if (fixture.Root != null)
                Object.Destroy(fixture.Root);
            if (fixture.Mesh != null)
                Object.Destroy(fixture.Mesh);
            if (binder != null)
                Object.Destroy(binder.gameObject);
        }

        private readonly struct CasterFixture
        {
            public CasterFixture(
                GameObject root,
                Mesh mesh,
                MeshRenderer renderer,
                DuelShadowCaster caster)
            {
                Root = root;
                Mesh = mesh;
                Renderer = renderer;
                Caster = caster;
            }

            public GameObject Root { get; }
            public Mesh Mesh { get; }
            public MeshRenderer Renderer { get; }
            public DuelShadowCaster Caster { get; }
        }
    }
}
