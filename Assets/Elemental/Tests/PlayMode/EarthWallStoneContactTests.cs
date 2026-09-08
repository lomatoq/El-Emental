using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthWallStoneContactTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private Scene _scene;
        private GameObject _stone;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath);
            EarthSceneReadinessGate gate = Find<EarthSceneReadinessGate>();
            Assert.That(gate, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 125d;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_stone != null) Object.Destroy(_stone);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator ThreeThrownStonesCrackThenEjectWallCellsWithForwardMomentum()
        {
            EarthWall wall = Find<EarthWallPool>().Acquire(new Vector3(-3f, 120f, 0f),
                new Vector3(3f, 120f, 0f), Vector3.zero, 3f, .55f, 8100u, Vector3.up);
            yield return new WaitForSeconds(1.5f);
            wall.Body.isKinematic = true;
            int originalBonds = wall.RemainingBondCount;
            Vector3 direction = -wall.transform.forward;
            float maximumEjection = 0f;
            for (int hit = 0; hit < 3; hit++)
            {
                _stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _stone.name = "Actual wall contact stone";
                _stone.transform.localScale = Vector3.one * .35f;
                _stone.transform.position = wall.transform.TransformPoint(new Vector3(0f, .15f, .5f)) - direction;
                Rigidbody body = _stone.AddComponent<Rigidbody>();
                body.mass = 40f; body.useGravity = false; body.linearDamping = 0f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = direction * 8f;
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    for (int piece = 0; piece < wall.StructureRuntime.PieceCount; piece++)
                        if (!wall.IsPieceStructurallySupported(piece))
                            maximumEjection = Mathf.Max(maximumEjection, Vector3.Dot(
                                wall.StructureRuntime.GetPieceRuntime(piece).Body.linearVelocity, direction));
                }
                Assert.That(wall.AcceptedPhysicalImpactCount, Is.EqualTo(hit + 1),
                    "One stone contact may touch several cells but must damage the graph once.");
                if (hit == 0) Assert.That(wall.RemainingBondCount, Is.EqualTo(originalBonds));
                Object.Destroy(_stone); _stone = null;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(wall.RemainingBondCount, Is.LessThan(originalBonds));
            Assert.That(maximumEjection, Is.GreaterThan(.15f),
                "Newly unsupported cells must inherit a bounded portion of the incoming stone momentum.");
            for (int bond = 0; bond < wall.StructureRuntime.BondCount; bond++)
                if (wall.StructureRuntime.GetBondDefinition(bond).PieceB == EarthBondGraph.WorldPieceIndex)
                    Assert.That(wall.StructureRuntime.GetBondState(bond).Phase, Is.Not.EqualTo(EarthBondPhase.Broken));
        }

        [UnityTest]
        public IEnumerator BothAuthoredPushBouldersUseSharedGravityMassAndFracture()
        {
            foreach (string name in new[] { "Light Push Boulder", "Heavy Push Boulder" })
            {
                Transform found = null;
                foreach (GameObject root in _scene.GetRootGameObjects())
                    if (root.name == "Magic Push Boulders") found = root.transform.Find(name);
                Assert.That(found, Is.Not.Null);
                EarthDestructibleDecorRock rock = found.GetComponent<EarthDestructibleDecorRock>();
                Assert.That(rock, Is.Not.Null, "Run Repair Two Push Boulders on the authored scene.");
                GravityBody gravity = found.GetComponent<GravityBody>();
                Assert.That(gravity.IsOperational, Is.True);
                Assert.That(rock.IsAnchored, Is.False);
                Assert.That(rock.EarthMass, Is.EqualTo(EarthMatterMassRuntime.ResolveFromCollider(
                    found.GetComponent<Collider>())).Within(.001f));
                Assert.That(found.GetComponent<PhysicalImpactTarget>(), Is.Null,
                    "A second generic hit owner must not bypass fracture/deduplication.");
                Vector3 up = found.position.normalized;
                rock.Body.position += up * 2f;
                rock.Body.linearVelocity = Vector3.zero;
                for (int frame = 0; frame < 5; frame++) yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(rock.Body.linearVelocity, up), Is.LessThan(-.1f));
                rock.ApplyImpact(found.position, up, 50000f);
                Assert.That(rock.IsShattered, Is.True);
            }
        }

        private T Find<T>() where T : Component
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
    }
}
