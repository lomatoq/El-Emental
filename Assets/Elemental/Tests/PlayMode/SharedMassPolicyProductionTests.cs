using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class SharedMassPolicyProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private Scene _scene;
        [UnitySetUp]
        public IEnumerator Load()
        {
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var gate = Find<EarthSceneReadinessGate>();
            double until = Time.realtimeSinceStartupAsDouble + 125d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>(true)) bot.enabled = false;
            Assert.That(Find<EarthMatterKernelBehaviour>().MassPolicyAsset, Is.Not.Null,
                "Run Elemental/Setup/Install Shared Stone Mass Policy and save production scene.");
        }
        [UnityTearDown]
        public IEnumerator Unload()
        { if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene); }

        [UnityTest]
        public IEnumerator ProductionDecorHeroAndWallUseOnePolicyAndWallChildrenConserveMass()
        {
            EarthMatterMassProfile policy = Find<EarthMatterKernelBehaviour>().MassPolicy;
            int rocks = 0;
            foreach (GameObject root in _scene.GetRootGameObjects())
            foreach (EarthDestructibleDecorRock rock in root.GetComponentsInChildren<EarthDestructibleDecorRock>(true))
            {
                Assert.That(rock.EarthMass, Is.EqualTo(EarthMatterMassRuntime.ResolveFromCollider(
                    rock.GetComponent<Collider>(), in policy)).Within(.01f), rock.name);
                rocks++;
            }
            Assert.That(rocks, Is.GreaterThan(0));
            var executor = Find<MagicExecutor>();
            var pool = Find<EarthFragmentPool>();
            Assert.That(pool.ResolveNewStoneMass(.1f), Is.EqualTo(executor.ResolveNewStoneMass(.1f)).Within(.001f));
            var wall = Find<EarthWallPool>().Acquire(new Vector3(-2, 120, 0), new Vector3(2, 120, 0),
                Vector3.zero, 2f, .4f, 0xAA7101u, Vector3.up);
            Assert.That(wall, Is.Not.Null);
            float mass = EarthMatterMassPolicy.ResolveGameplayMass(wall.SolidVolume, in policy);
            Assert.That(wall.Body.mass, Is.EqualTo(mass).Within(.001f));
            yield return new WaitForSeconds(1.5f);
            Assert.That(wall.ApplyRockImpact(wall.transform.position, wall.transform.forward, 10000f), Is.True);
            float bodies = 0f, records = 0f, volume = 0f;
            for (int i = 0; i < wall.StructureRuntime.PieceCount; i++)
            {
                EarthPieceRuntime piece = wall.StructureRuntime.GetPieceRuntime(i);
                bodies += piece.Body.mass;
                Assert.That(piece.MatterIdentity.TryRead(out EarthMatterRecord record), Is.True);
                Assert.That(piece.Body.mass, Is.EqualTo(record.Mass).Within(.001f), "Physical/canonical child mass differs.");
                records += record.Mass; volume += record.Volume;
            }
            Assert.That(bodies, Is.EqualTo(mass).Within(.01f));
            Assert.That(records, Is.EqualTo(mass).Within(.01f));
            Assert.That(volume, Is.EqualTo(wall.SolidVolume).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ActualTerrainExtractionAccretionAndHeldSplitKeepCanonicalVolumeAndMass()
        {
            var executor = Find<MagicExecutor>();
            var planet = executor.PlanetCenterTransform;
            Vector3 up = Vector3.up;
            Vector3 origin = planet.position + up * 80f;
            Collider collider = planet.GetComponent<Collider>();
            Vector3 point = collider != null ? collider.ClosestPoint(origin) : planet.position + up * 52f;
            var command = new MagicCommand(0xAA7201u, 1, ElementId.Earth, EarthAbilityIds.PullRock,
                (float3)point, (float3)up, new[] { (float3)point }, .1f, 0, 0xAA72u);
            Assert.That(executor.Execute(in command), Is.True);
            double until = Time.realtimeSinceStartupAsDouble + 20d;
            while ((executor.HasPendingExtraction || executor.HeldFragment == null) && Time.realtimeSinceStartupAsDouble < until)
                yield return null;
            EarthFragment source = executor.HeldFragment;
            Assert.That(source, Is.Not.Null);
            EarthMatterMassProfile policy = executor.MassPolicy;
            var identity = source.GetComponent<EarthMatterIdentity>();
            Assert.That(identity.TryRead(out EarthMatterRecord original), Is.True);
            Assert.That(original.Volume, Is.EqualTo(source.SolidVolume).Within(.00001f));
            Assert.That(original.Mass, Is.EqualTo(EarthMatterMassPolicy.ResolveGameplayMass(original.Volume, in policy)).Within(.001f));
            // The accretion adapter receives an already reserved terrain volume.
            // The domain proof verifies its ledger update independently of chip timing.
            source.AccreteVolume(.01f);
            Assert.That(identity.TryRead(out EarthMatterRecord grown), Is.True);
            Assert.That(grown.Volume, Is.EqualTo(original.Volume + .01f).Within(.00001f));
            Assert.That(source.Mass, Is.EqualTo(grown.Mass).Within(.001f));
            Assert.That(executor.TryFractureHeldBoulder(), Is.True);
            Assert.That(identity.Kernel.TryGet(grown.Id, out EarthMatterRecord consumed), Is.True);
            Assert.That(consumed.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
            int count = 0; float mass = 0, volume = 0;
            foreach (GameObject root in _scene.GetRootGameObjects())
            foreach (EarthFragment fragment in root.GetComponentsInChildren<EarthFragment>(true))
            {
                var child = fragment.GetComponent<EarthMatterIdentity>();
                if (child == null || !child.TryRead(out EarthMatterRecord record) || record.Phase == EarthMatterPhase.Consumed ||
                    record.Source.Kind != EarthSourceKind.Fragment || record.Source.SourceStableId != grown.Id.StableId) continue;
                count++; mass += fragment.Mass; volume += record.Volume;
                Assert.That(fragment.Mass, Is.EqualTo(record.Mass).Within(.001f));
                Assert.That(fragment.SolidVolume, Is.EqualTo(record.Volume).Within(.00001f));
            }
            Assert.That(count, Is.EqualTo(4));
            Assert.That(mass, Is.EqualTo(grown.Mass).Within(.001f));
            Assert.That(volume, Is.EqualTo(grown.Volume).Within(.00001f));
        }

        private T Find<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
    }
}
