using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPhysicalCompletionRuntimeTests
    {
        private const string EarthCoreScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const float NearMissSpeed = 60f;
        private const float NearMissClearance = 0.075f;

        [UnityTest]
        public IEnumerator ArenaAndDecorNearMissesDoNotChipOrFractureWithoutContact()
        {
            Scene scene = default;
            AsyncOperation unload = null;
            EarthMvpBotController bot = null;
            bool botWasEnabled = false;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    EarthCoreScenePath,
                    LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                scene = SceneManager.GetSceneByPath(EarthCoreScenePath);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                bot = FindInScene<EarthMvpBotController>(scene);
                if (bot != null)
                {
                    botWasEnabled = bot.enabled;
                    bot.enabled = false;
                }

                MagicExecutor executor = FindInScene<MagicExecutor>(scene);
                EarthFragmentPool fragmentPool = FindInScene<EarthFragmentPool>(scene);
                EarthDestructibleDecorRock decor = FindAnchoredDecor(scene);
                EarthArenaStructure arena = FindOrdinaryArenaStructure(scene);
                Assert.That(executor, Is.Not.Null);
                Assert.That(fragmentPool, Is.Not.Null);
                Assert.That(decor, Is.Not.Null,
                    "The production near-miss gate needs one intact authored decor rock.");
                Assert.That(arena, Is.Not.Null,
                    "The production near-miss gate needs one ordinary-damage arena structure.");

                yield return AssertNearMissDoesNotMutate(
                    fragmentPool,
                    executor,
                    decor.GetComponent<Collider>(),
                    decor,
                    null,
                    "decor rock");
                yield return AssertNearMissDoesNotMutate(
                    fragmentPool,
                    executor,
                    arena.GetComponent<Collider>(),
                    null,
                    arena,
                    "arena structure");
            }
            finally
            {
                if (bot != null) bot.enabled = botWasEnabled;
                if (scene.IsValid() && scene.isLoaded)
                    unload = SceneManager.UnloadSceneAsync(scene);
            }

            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator ReleasedEnvironmentFamiliesKeepFiniteInwardRadialGravity()
        {
            Scene scene = default;
            AsyncOperation unload = null;
            EarthMvpBotController bot = null;
            bool botWasEnabled = false;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    EarthCoreScenePath,
                    LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                scene = SceneManager.GetSceneByPath(EarthCoreScenePath);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                bot = FindInScene<EarthMvpBotController>(scene);
                if (bot != null)
                {
                    botWasEnabled = bot.enabled;
                    bot.enabled = false;
                }

                MagicExecutor executor = FindInScene<MagicExecutor>(scene);
                EarthFragmentPool fragmentPool = FindInScene<EarthFragmentPool>(scene);
                EarthRockDebrisPool debrisPool = FindInScene<EarthRockDebrisPool>(scene);
                GravityWorldBehaviour gravityWorld = fragmentPool != null
                    ? fragmentPool.GravityWorld
                    : null;
                Assert.That(executor, Is.Not.Null);
                Assert.That(fragmentPool, Is.Not.Null);
                Assert.That(debrisPool, Is.Not.Null);
                Assert.That(gravityWorld, Is.Not.Null);

                var probes = new List<GravityProbe>(6);
                var targets = new IEarthPhysicalTarget[64];

                EarthArenaStructure arena = FindOrdinaryArenaStructure(scene);
                Assert.That(arena, Is.Not.Null);
                var arenaImpact = new EarthStructureImpact(
                    arena.transform.position,
                    arena.transform.forward,
                    1200f,
                    EarthStructureImpactKind.Projectile,
                    0xF101u);
                Assert.That(arena.ApplyEarthImpact(in arenaImpact), Is.True,
                    "The arena fixture did not release a physical cell.");
                yield return new WaitForFixedUpdate();
                AddFirstDynamicTarget(probes, targets, arena.CopyActiveTargetsNonAlloc(targets),
                    "arena piece");

                EarthWall wall = FindActiveInScene<EarthWall>(scene);
                Assert.That(wall, Is.Not.Null,
                    "The shipping scene needs one active generated wall for the gravity gate.");
                Assert.That(wall.ApplyRockImpact(
                    wall.transform.position,
                    wall.transform.forward,
                    5000f), Is.True);
                yield return new WaitForFixedUpdate();
                AddFirstDynamicTarget(probes, targets, wall.CopyActiveTargetsNonAlloc(targets),
                    "wall piece");

                EarthPlatform platform = FindActiveInScene<EarthPlatform>(scene);
                Assert.That(platform, Is.Not.Null,
                    "The shipping scene needs one active generated platform for the gravity gate.");
                for (int frame = 0;
                     frame < 180 &&
                     platform.PreparationPhase != EarthPlatformPreparationPhase.FractureReady;
                     frame++)
                    yield return null;
                Assert.That(platform.PreparationPhase,
                    Is.EqualTo(EarthPlatformPreparationPhase.FractureReady));
                Assert.That(platform.ApplyStructureImpact(
                    platform.SurfaceTopPoint,
                    platform.transform.forward,
                    10000f), Is.True);
                for (int tick = 0; tick < 8 && !platform.IsFractured; tick++)
                    yield return new WaitForFixedUpdate();
                Assert.That(platform.IsFractured, Is.True);
                AddFirstDynamicTarget(probes, targets, platform.CopyActiveTargetsNonAlloc(targets),
                    "platform piece");

                EarthDestructibleDecorRock decor = FindAnchoredDecor(scene);
                Assert.That(decor, Is.Not.Null);
                decor.OnEarthMagicGrabbed(EarthMagicGripKind.VectorField);
                Assert.That(decor.IsAnchored, Is.False,
                    "The decor fixture did not enter its released physical state.");
                probes.Add(Probe("decor rock", decor.Body));

                Vector3 fragmentUp = SafeUp(decor.Body.position, gravityWorld.transform.position);
                EarthFragment fragment = fragmentPool.Acquire(
                    executor,
                    decor.Body.position + fragmentUp * 3f,
                    0.22f,
                    18f);
                Assert.That(fragment, Is.Not.Null);
                probes.Add(Probe("hero fragment", fragment.Body));

                Vector3 debrisOrigin = fragment.Body.position + fragmentUp * 2f;
                debrisPool.EmitShatter(
                    debrisOrigin,
                    fragmentUp,
                    Vector3.zero,
                    0.35f,
                    18f,
                    0xF102u);
                EarthRockDebris debris = FindActiveInScene<EarthRockDebris>(scene);
                Assert.That(debris, Is.Not.Null,
                    "The production debris pool did not expose one ballistic piece.");
                probes.Add(Probe("pooled debris", debris.GetComponent<Rigidbody>()));

                Assert.That(probes, Has.Count.EqualTo(6));
                Vector3 center = gravityWorld.transform.position;
                for (int index = 0; index < probes.Count; index++)
                {
                    GravityProbe probe = probes[index];
                    Vector3 up = SafeUp(probe.Body.worldCenterOfMass, center);
                    Vector3 tangent = Vector3.Cross(up, Vector3.forward);
                    if (tangent.sqrMagnitude < 0.25f)
                        tangent = Vector3.Cross(up, Vector3.right);
                    tangent.Normalize();
                    probe.Body.position = center + up * (70f + index * 1.5f) + tangent * index * 3f;
                    probe.Body.linearVelocity = Vector3.zero;
                    probe.Body.angularVelocity = Vector3.zero;
                    probe.Body.WakeUp();
                }
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                for (int index = 0; index < probes.Count; index++)
                    AssertLiveGravity(probes[index], center);
            }
            finally
            {
                if (bot != null) bot.enabled = botWasEnabled;
                if (scene.IsValid() && scene.isLoaded)
                    unload = SceneManager.UnloadSceneAsync(scene);
            }

            if (unload != null) yield return unload;
        }

        private static IEnumerator AssertNearMissDoesNotMutate(
            EarthFragmentPool fragmentPool,
            MagicExecutor executor,
            Collider targetCollider,
            EarthDestructibleDecorRock decor,
            EarthArenaStructure arena,
            string label)
        {
            Assert.That(targetCollider, Is.Not.Null, label);
            bool anchoredBefore = decor != null && decor.IsAnchored;
            bool shatteredBefore = decor != null && decor.IsShattered;
            uint generationBefore = arena != null ? arena.Generation : 0u;
            int releasedBefore = arena != null ? arena.ReleasedPieceCount : 0;

            Vector3 center = targetCollider.bounds.center;
            Vector3 up = SafeUp(center, Vector3.zero);
            Vector3 tangent = Vector3.ProjectOnPlane(targetCollider.transform.right, up);
            if (tangent.sqrMagnitude < 0.25f)
                tangent = Vector3.ProjectOnPlane(targetCollider.transform.forward, up);
            tangent.Normalize();
            float extentAlongUp = ProjectedExtent(targetCollider.bounds.extents, up);
            const float radius = 0.16f;
            float fixedTravel = NearMissSpeed * Time.fixedDeltaTime;
            Vector3 start = center - tangent * (fixedTravel * 2f) +
                            up * (extentAlongUp + radius + NearMissClearance);
            EarthFragment fragment = fragmentPool.Acquire(executor, start, radius, 18f);
            Assert.That(fragment, Is.Not.Null, label);
            Collider projectileCollider = fragment.GetComponent<Collider>();
            Assert.That(projectileCollider, Is.Not.Null, label);

            int acceptedImpacts = 0;
            void OnImpact(EarthProjectileSurfaceImpact _) => acceptedImpacts++;
            fragment.SurfaceImpactAccepted += OnImpact;
            float closestCenterDistance = float.PositiveInfinity;
            bool penetrated = false;
            try
            {
                fragment.LaunchProjectile(tangent, NearMissSpeed, null, 0f);
                for (int tick = 0; tick < 5; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    Vector3 bodyPosition = fragment.Body.position;
                    closestCenterDistance = Mathf.Min(
                        closestCenterDistance,
                        Vector3.Distance(bodyPosition, targetCollider.ClosestPoint(bodyPosition)));
                    penetrated |= Physics.ComputePenetration(
                        projectileCollider,
                        projectileCollider.transform.position,
                        projectileCollider.transform.rotation,
                        targetCollider,
                        targetCollider.transform.position,
                        targetCollider.transform.rotation,
                        out _,
                        out _);
                }

                Assert.That(penetrated, Is.False,
                    $"The {label} near-miss fixture physically overlapped its target.");
                Assert.That(closestCenterDistance,
                    Is.InRange(radius + 0.01f, radius + 0.30f),
                    $"The {label} path must be a measured near-miss, not a distant bypass.");
                Assert.That(acceptedImpacts, Is.Zero,
                    $"The {label} near-miss emitted a gameplay impact without collision.");
                Assert.That(fragment.gameObject.activeSelf, Is.True,
                    $"The {label} near-miss consumed the live projectile.");
                if (decor != null)
                {
                    Assert.That(decor.IsAnchored, Is.EqualTo(anchoredBefore));
                    Assert.That(decor.IsShattered, Is.EqualTo(shatteredBefore));
                }
                if (arena != null)
                {
                    Assert.That(arena.Generation, Is.EqualTo(generationBefore));
                    Assert.That(arena.ReleasedPieceCount, Is.EqualTo(releasedBefore));
                }
            }
            finally
            {
                fragment.SurfaceImpactAccepted -= OnImpact;
                fragment.gameObject.SetActive(false);
            }
        }

        private static void AddFirstDynamicTarget(
            List<GravityProbe> probes,
            IEarthPhysicalTarget[] targets,
            int count,
            string label)
        {
            for (int index = 0; index < count; index++)
            {
                Rigidbody body = targets[index]?.Body;
                if (body == null || body.isKinematic || !body.gameObject.activeInHierarchy) continue;
                probes.Add(Probe(label, body));
                return;
            }
            Assert.Fail($"The {label} fixture did not expose a released dynamic Rigidbody.");
        }

        private static GravityProbe Probe(string label, Rigidbody body)
        {
            Assert.That(body, Is.Not.Null, label);
            GravityBody gravity = body.GetComponent<GravityBody>();
            Assert.That(gravity, Is.Not.Null, $"{label} has no radial GravityBody.");
            Assert.That(gravity.enabled, Is.True, $"{label} radial gravity is disabled after release.");
            Assert.That(body.isKinematic, Is.False, $"{label} remained kinematic after release.");
            return new GravityProbe(label, body, gravity);
        }

        private static void AssertLiveGravity(GravityProbe probe, Vector3 center)
        {
            Vector3 acceleration = probe.Gravity.LastAcceleration;
            Vector3 velocity = probe.Body.linearVelocity;
            Vector3 inward = center - probe.Body.worldCenterOfMass;
            Assert.That(IsFinite(acceleration), Is.True, $"{probe.Label} acceleration is non-finite.");
            Assert.That(IsFinite(velocity), Is.True, $"{probe.Label} velocity is non-finite.");
            Assert.That(acceleration.magnitude, Is.GreaterThan(0.1f),
                $"{probe.Label} entered zero-G after release.");
            Assert.That(Vector3.Dot(acceleration, inward), Is.GreaterThan(0f),
                $"{probe.Label} gravity does not point toward the planet.");
            Assert.That(Vector3.Dot(velocity, inward.normalized), Is.GreaterThan(0.001f),
                $"{probe.Label} did not acquire inward velocity after two fixed ticks.");
        }

        private static EarthDestructibleDecorRock FindAnchoredDecor(Scene scene)
        {
            EarthDestructibleDecorRock[] rocks = FindAllInScene<EarthDestructibleDecorRock>(scene);
            for (int index = 0; index < rocks.Length; index++)
                if (rocks[index] != null && rocks[index].gameObject.activeInHierarchy &&
                    rocks[index].IsAnchored && !rocks[index].IsShattered &&
                    rocks[index].GetComponent<Collider>() != null)
                    return rocks[index];
            return null;
        }

        private static EarthArenaStructure FindOrdinaryArenaStructure(Scene scene)
        {
            EarthArenaStructure[] structures = FindAllInScene<EarthArenaStructure>(scene);
            for (int index = 0; index < structures.Length; index++)
                if (structures[index] != null && structures[index].gameObject.activeInHierarchy &&
                    structures[index].OrdinaryDamageEnabled &&
                    structures[index].GetComponent<Collider>() != null)
                    return structures[index];
            return null;
        }

        private static T FindActiveInScene<T>(Scene scene) where T : Component
        {
            T[] values = FindAllInScene<T>(scene);
            for (int index = 0; index < values.Length; index++)
                if (values[index] != null && values[index].gameObject.activeInHierarchy)
                    return values[index];
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] values = FindAllInScene<T>(scene);
            return values.Length > 0 ? values[0] : null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            var output = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                output.AddRange(roots[index].GetComponentsInChildren<T>(true));
            return output.ToArray();
        }

        private static float ProjectedExtent(Vector3 extents, Vector3 direction) =>
            Mathf.Abs(direction.x) * extents.x +
            Mathf.Abs(direction.y) * extents.y +
            Mathf.Abs(direction.z) * extents.z;

        private static Vector3 SafeUp(Vector3 position, Vector3 center)
        {
            Vector3 up = position - center;
            return up.sqrMagnitude > 0.25f ? up.normalized : Vector3.up;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private readonly struct GravityProbe
        {
            public GravityProbe(string label, Rigidbody body, GravityBody gravity)
            {
                Label = label;
                Body = body;
                Gravity = gravity;
            }

            public string Label { get; }
            public Rigidbody Body { get; }
            public GravityBody Gravity { get; }
        }
    }
}
