using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.PlayMode
{
    public sealed class ActiveRagdollRuntimeTests
    {
        [UnityTest]
        public IEnumerator EarthCorePuppetRemainsGroundedWhenPlayerProvidesNoInput()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
            Assert.That(puppet, Is.Not.Null);
            Rigidbody body = puppet.GetComponent<Rigidbody>();
            float startRadius = body.position.magnitude;
            float peakSpeed = 0f;
            float peakRadius = startRadius;
            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                peakSpeed = Mathf.Max(peakSpeed, body.linearVelocity.magnitude);
                peakRadius = Mathf.Max(peakRadius, body.position.magnitude);
            }

            Assert.That(peakRadius, Is.LessThan(startRadius + 1.5f),
                $"Idle active ragdoll escaped the planet; peak speed was {peakSpeed:0.00} m/s.");
            Assert.That(body.position.magnitude, Is.LessThan(26.5f));
            Assert.That(body.linearVelocity.magnitude, Is.LessThan(2f));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator CharacterFeelLab_RepeatedImpactsRemainFinite()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/CharacterFeelLab.unity",
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            ActiveRagdollPuppet puppet = Object.FindAnyObjectByType<ActiveRagdollPuppet>();
            CharacterFeelLabDriver driver = Object.FindAnyObjectByType<CharacterFeelLabDriver>();
            Assert.That(puppet, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);

            for (int tick = 0; tick < 300; tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Rigidbody rootBody = puppet.GetComponent<Rigidbody>();
            Assert.That(driver.PulseCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(IsFinite(rootBody.position), Is.True);
            Assert.That(IsFinite(rootBody.linearVelocity), Is.True);
            Assert.That(IsFinite(rootBody.angularVelocity), Is.True);
            Assert.That(float.IsFinite(puppet.CurrentState.StaggerDebt), Is.True);
            Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);

            ActiveRagdollJoint[] joints = Object.FindObjectsByType<ActiveRagdollJoint>();
            Assert.That(joints.Length, Is.GreaterThanOrEqualTo(6));
            for (int index = 0; index < joints.Length; index++)
            {
                Assert.That(IsFinite(joints[index].Body.linearVelocity), Is.True);
                Assert.That(IsFinite(joints[index].Body.angularVelocity), Is.True);
                Assert.That(joints[index].Body.angularVelocity.magnitude, Is.LessThan(100f));
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/CharacterFeelLab.unity"));
            if (unload != null)
            {
                yield return unload;
            }
        }

        [UnityTest]
        public IEnumerator PuppetDisablesMotorDrivesOnRagdollAndRecoversWithoutExplosion()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ragdoll Test Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Ragdoll Runtime Test Root";
            root.SetActive(false);
            root.transform.position = new Vector3(0f, 1f, 0f);
            Rigidbody rootBody = root.AddComponent<Rigidbody>();
            rootBody.isKinematic = true;
            PhysicalImpactTarget impact = root.AddComponent<PhysicalImpactTarget>();
            impact.Configure(rootBody);

            GameObject targetObject = new GameObject("Chest Pose Target");
            targetObject.transform.SetParent(root.transform, false);
            targetObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);

            GameObject chestObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestObject.name = "Physical Chest";
            chestObject.transform.SetParent(root.transform, false);
            chestObject.transform.localPosition = targetObject.transform.localPosition;
            Rigidbody chestBody = chestObject.AddComponent<Rigidbody>();
            chestBody.mass = 5f;
            chestBody.useGravity = false;
            ConfigurableJoint configurableJoint = chestObject.AddComponent<ConfigurableJoint>();
            configurableJoint.connectedBody = rootBody;
            ActiveRagdollJoint joint = chestObject.AddComponent<ActiveRagdollJoint>();
            joint.Configure(chestBody, configurableJoint, targetObject.transform, 500f, 50f, 800f, 45f);

            ActiveRagdollPuppet puppet = root.AddComponent<ActiveRagdollPuppet>();
            puppet.Configure(
                1u,
                null,
                rootBody,
                null,
                impact,
                chestObject.transform,
                new[] { joint },
                new[] { root.GetComponent<Collider>(), chestObject.GetComponent<Collider>() });
            root.SetActive(true);

            for (int index = 0; index < 200; index++)
            {
                puppet.InjectImpact(20f);
            }

            yield return new WaitForFixedUpdate();
            Assert.That(puppet.CurrentState.Mode, Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
            Assert.That(configurableJoint.slerpDrive.maximumForce, Is.EqualTo(0f).Within(0.001f));

            for (int index = 0; index < 70; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(puppet.CurrentState.Mode, Is.EqualTo(CharacterPhysicalMode.AnimatedMotor));
            Assert.That(puppet.CurrentState.MuscleStrength, Is.EqualTo(1f).Within(0.001f));
            Assert.That(configurableJoint.slerpDrive.maximumForce, Is.GreaterThan(0f));
            Assert.That(float.IsFinite(puppet.MaximumJointError), Is.True);
            Assert.That(float.IsFinite(chestBody.angularVelocity.x), Is.True);
            Assert.That(chestBody.angularVelocity.magnitude, Is.LessThan(50f));

            Object.Destroy(root);
            Object.Destroy(floor);
            yield return null;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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
