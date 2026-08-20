using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSurfRuntimeTests
    {
        [UnityTest]
        public IEnumerator ShiftMovementSurfFindsSupportEmergesAndMovesTangentially()
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Surf Test Planet";
            planet.transform.localScale = Vector3.one * 48f;
            Physics.SyncTransforms();

            GameObject casterObject = new GameObject("Surf Test Caster");
            casterObject.transform.position = Vector3.up * 24.9f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.isKinematic = true;
            CapsuleCollider casterCollider = casterObject.AddComponent<CapsuleCollider>();
            casterCollider.height = 1.8f;
            casterCollider.radius = 0.35f;

            EarthSurfController surf = casterObject.AddComponent<EarthSurfController>();
            surf.Configure(caster, null, planet.transform, null, null);
            Physics.SyncTransforms();
            Assert.That(surf.HasNearbyStartSurface(), Is.True,
                "Shift+movement must accept a stable planet surface even if the motor ground bit flickers.");
            Assert.That(surf.Begin(Time.fixedUnscaledTime, Vector3.forward), Is.True);
            Vector3 startedAt = GameObject.Find("Earth Surf Plough").transform.position;

            for (int frame = 0; frame < 12; frame++) yield return new WaitForFixedUpdate();

            GameObject board = GameObject.Find("Earth Surf Plough");
            Assert.That(board, Is.Not.Null);
            Assert.That(surf.IsActive, Is.True);
            Assert.That(surf.Speed, Is.GreaterThanOrEqualTo(4f));
            Mesh boardMesh = board.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(boardMesh.bounds.size.x, Is.GreaterThan(2.2f));
            Assert.That(boardMesh.bounds.size.z, Is.GreaterThan(3.7f),
                "The surf spell needs a readable plough platform, not a tiny foot wedge.");
            Assert.That(Vector3.Distance(startedAt, board.transform.position), Is.GreaterThan(0.45f),
                "The plough must actually carry forward, not only spawn under the character.");
            Assert.That(Mathf.Abs(Vector3.Distance(board.transform.position, planet.transform.position) - 24f),
                Is.LessThan(0.6f), "Surf movement must follow planet curvature.");
            int activeCutChips = 0;
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name.StartsWith("Surf Cut Chip") &&
                    transforms[index].gameObject.activeSelf) activeCutChips++;
            Assert.That(activeCutChips, Is.GreaterThanOrEqualTo(3),
                "The plough needs a dense spray of moving cut stones, not one occasional chip.");

            surf.Release(Time.fixedUnscaledTime);
            for (int frame = 0; frame < 30; frame++) yield return new WaitForFixedUpdate();
            Assert.That(surf.IsActive, Is.False);

            Object.Destroy(casterObject);
            Object.Destroy(planet);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionPloughCarriesTheVisibleHeroInsteadOfLeavingHimBehind()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthPolishLab.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthSurfController surf = FindInScene<EarthSurfController>(scene);
            PlanetMotor motor = surf != null ? surf.GetComponent<PlanetMotor>() : null;
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            ActiveRagdollPuppet puppet = motor != null ? motor.GetComponent<ActiveRagdollPuppet>() : null;
            Animator animator = motor != null ? motor.GetComponentInChildren<Animator>(true) : null;
            Assert.That(surf, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            Vector3 startedAt = body.worldCenterOfMass;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : motor.transform.up;
            Assert.That(surf.Begin(Time.fixedUnscaledTime, motor.FacingForward), Is.True);
            for (int tick = 0; tick < 36; tick++) yield return new WaitForFixedUpdate();

            Vector3 displacement = body.worldCenterOfMass - startedAt;
            float tangentTravel = Vector3.ProjectOnPlane(displacement, up).magnitude;
            Assert.That(tangentTravel, Is.GreaterThan(0.8f),
                "Shift+movement must carry the hero with the wedge; moving only the effect is not gameplay.");
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(surf.SurfaceId));
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.GetBool("Surfing"), Is.True);
            AnimatorStateInfo surfState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(surfState.IsName("Surf Crouch") || surfState.IsName("Surf Enter") ||
                        surfState.IsName("Base Layer.Surf Crouch") || surfState.IsName("Base Layer.Surf Enter"),
                Is.True, "The surf wedge must own a crouched base pose instead of a T-pose or walking legs.");
            if (puppet != null)
                Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(
                    Elemental.Simulation.Characters.CharacterPhysicalMode.FullRagdoll));

            surf.Cancel();
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
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
    }
}
