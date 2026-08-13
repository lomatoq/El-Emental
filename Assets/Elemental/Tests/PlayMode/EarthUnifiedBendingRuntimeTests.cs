using System.Collections;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthUnifiedBendingRuntimeTests
    {
        [UnityTest]
        public IEnumerator LmbContractExtractsFromTerrainControlsDynamicMassAndPreservesReleaseMotion()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Camera camera = FindInScene<Camera>(scene);
            Collider proxy = FindByName(scene, "Planet Collision Proxy")?.GetComponent<Collider>();
            Physics.SyncTransforms();
            Assert.That(TryFindSurfacePoint(camera, proxy, out float2 screenPoint, out Vector3 surface), Is.True);

            int editsBefore = planet.State.EditCount;
            bool began = input.TryBeginEarthBendAtScreenPoint(
                screenPoint,
                BendOriginMode.Aim,
                0.62f);
            EarthFragment fragment = executor.HeldFragment;
            Vector3 localUp = (surface - proxy.bounds.center).normalized;
            float signedEmergence = fragment != null
                ? Vector3.Dot(fragment.transform.position - surface, localUp)
                : float.PositiveInfinity;

            Assert.That(began, Is.True);
            Assert.That(planet.State.EditCount, Is.EqualTo(editsBefore + 1));
            Assert.That(fragment, Is.Not.Null);
            Assert.That(fragment.Body.isKinematic, Is.False);
            Assert.That(signedEmergence, Is.LessThan(0f),
                "The mass must begin inside the selected flat terrain volume, not pop into the air.");

            float2 movedPointer = screenPoint + new float2(90f, 35f);
            Assert.That(input.TrySetEarthBendTargetAtScreenPoint(movedPointer, 1f / 60f), Is.True);
            Vector3 worldTargetBeforeAnchorMove = fragment.BendTargetPosition;
            Transform legacyAnchor = FindByName(scene, "Held Earth Anchor")?.transform;
            Assert.That(legacyAnchor, Is.Not.Null);
            legacyAnchor.localPosition += Vector3.right * 4f;
            for (int index = 0; index < 8; index++) yield return new WaitForFixedUpdate();
            Assert.That(fragment.LastAppliedControlForce.sqrMagnitude, Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(fragment.BendTargetPosition, worldTargetBeforeAnchorMove), Is.LessThan(0.01f),
                "Explicit world bending must not be overwritten by an anchor moving with the caster.");

            bool released = input.TryReleaseEarthBendAtScreenPoint(
                movedPointer,
                new Vector3(4f, 1.5f, 0f),
                BendGestureIntent.Flick,
                out Vector3 releaseVelocity);
            yield return new WaitForFixedUpdate();

            Assert.That(released, Is.True);
            Assert.That(fragment.IsHeld, Is.False);
            Assert.That(fragment.Body.isKinematic, Is.False);
            Assert.That(releaseVelocity.x, Is.GreaterThan(0f));
            Assert.That(input.CurrentBendPhase, Is.EqualTo(BendPhase.Idle));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator LmbOnExistingStoneAcquiresItWithoutEditingPlanetVoxels()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Camera camera = FindInScene<Camera>(scene);
            Rigidbody stone = FindByName(scene, "Light Push Boulder")?.GetComponent<Rigidbody>();
            Assert.That(input, Is.Not.Null);
            Assert.That(stone, Is.Not.Null);
            Physics.SyncTransforms();
            Vector3 projected = camera.WorldToScreenPoint(stone.worldCenterOfMass);
            Assert.That(projected.z, Is.GreaterThan(0f));
            int editsBefore = planet.State.EditCount;

            bool acquired = input.TryBeginEarthBendAtScreenPoint(
                new float2(projected.x, projected.y),
                BendOriginMode.Aim,
                0.4f);
            Assert.That(acquired, Is.True);
            Assert.That(executor.HeldBody, Is.SameAs(stone));
            Assert.That(planet.State.EditCount, Is.EqualTo(editsBefore));

            float2 movedPointer = new float2(projected.x + 75f, projected.y + 25f);
            Assert.That(input.TrySetEarthBendTargetAtScreenPoint(movedPointer, 1f / 60f), Is.True);
            for (int frame = 0; frame < 10; frame++) yield return new WaitForFixedUpdate();
            Assert.That(executor.HeldControlForce.sqrMagnitude, Is.GreaterThan(1f));

            Assert.That(input.TryReleaseEarthBendAtScreenPoint(
                movedPointer,
                new Vector3(3f, 1f, 0f),
                BendGestureIntent.Flick,
                out Vector3 releaseVelocity), Is.True);
            Assert.That(executor.HeldBody, Is.Null);
            Assert.That(releaseVelocity.sqrMagnitude, Is.GreaterThan(1f));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static bool TryFindSurfacePoint(
            Camera camera,
            Collider proxy,
            out float2 screenPoint,
            out Vector3 surface)
        {
            screenPoint = default;
            surface = default;
            if (camera == null || proxy == null) return false;
            int width = Mathf.Max(320, Screen.width);
            int height = Mathf.Max(200, Screen.height);
            for (int y = 16; y < height - 16; y += 8)
            for (int x = 16; x < width - 16; x += 8)
            {
                if (!proxy.Raycast(camera.ScreenPointToRay(new Vector2(x, y)), out RaycastHit hit, 200f))
                    continue;
                screenPoint = new float2(x, y);
                surface = hit.point;
                return true;
            }
            return false;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
