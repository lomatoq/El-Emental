using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Input.Gestures;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPillarMobilityTests
    {
        [UnityTest]
        public IEnumerator HeldSpaceContractRaisesOnePillarAndLaunchesAlongLocalUp()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthPillarMobility mobility = FindInScene<EarthPillarMobility>(scene);
            PlanetMotor motor = FindInScene<PlanetMotor>(scene);
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            GameObject feedback = FindByName(scene, "Earth Pillar Feedback");
            for (int frame = 0; frame < 100 && motor != null && !motor.IsGrounded; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(mobility, Is.Not.Null);
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(mobility.BeginCharge(), Is.True);
            yield return new WaitForSecondsRealtime(0.32f);
            float visibleCharge = mobility.Charge01;
            Vector3 startPosition = body.position;
            EarthPillarLaunchEvent raised = default;
            bool emitted = false;
            mobility.PillarRaised += value => { raised = value; emitted = true; };
            Assert.That(mobility.ReleaseCharge(), Is.True);
            yield return null;
            bool pillarVisible = FindByName(scene, "Rising Earth Pillar")?.activeSelf == true;
            int riseTicks = Mathf.CeilToInt(raised.RiseSeconds / Time.fixedDeltaTime) + 3;
            for (int frame = 0; frame < riseTicks; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(visibleCharge, Is.GreaterThan(0.05f));
            Assert.That(emitted, Is.True);
            Assert.That(raised.Height, Is.GreaterThan(1.5f));
            Assert.That(pillarVisible, Is.True);
            Assert.That(Vector3.Dot(body.position - startPosition, motor.LocalUp), Is.GreaterThan(0.35f));
            Assert.That(Vector3.Dot(body.linearVelocity, motor.LocalUp), Is.GreaterThan(4f));
            Assert.That(feedback, Is.Not.Null);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator CameraFollowsCharacterHeadingWithoutConsumingThePointer()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            PlanetMotor motor = FindInScene<PlanetMotor>(scene);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            input.enabled = false;
            Elemental.Presentation.Camera.PlanetCameraRig cameraRig =
                FindInScene<Elemental.Presentation.Camera.PlanetCameraRig>(scene);
            yield return null;
            Vector3 initialHeading = cameraRig.TangentForward;
            Vector3 initialCameraPosition = cameraRig.transform.position;
            Vector3 desired = Quaternion.AngleAxis(55f, motor.LocalUp) * initialHeading;
            motor.SetAimDirection(desired);
            for (int frame = 0; frame < 55; frame++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }

            Vector3 actual = Vector3.ProjectOnPlane(motor.transform.forward, motor.LocalUp).normalized;
            float cameraHeight = Vector3.Dot(
                cameraRig.transform.position - motor.transform.position,
                motor.LocalUp);
            float focusAhead = Vector3.Dot(
                cameraRig.SmoothedFocus - motor.transform.position,
                cameraRig.TangentForward);
            Assert.That(Vector3.Angle(initialHeading, cameraRig.TangentForward), Is.GreaterThan(30f));
            Assert.That(Vector3.Distance(initialCameraPosition, cameraRig.transform.position), Is.GreaterThan(1f));
            Assert.That(Vector3.Dot(actual, desired), Is.GreaterThan(0.88f));
            Assert.That(cameraHeight, Is.InRange(1.6f, 3.2f),
                "The Earth MVP camera should stay close enough to read the caster's body.");
            Assert.That(focusAhead, Is.GreaterThan(2.4f));

            yield return SceneManager.UnloadSceneAsync(scene);
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
