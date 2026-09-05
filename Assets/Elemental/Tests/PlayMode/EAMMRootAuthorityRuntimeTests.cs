using System.Collections;
using System.Reflection;
using Elemental.Input.Actions;
using Elemental.Presentation.Animation;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EAMMRootAuthorityRuntimeTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private sealed class ScriptedMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;

            public PlanetMotorCommand SampleCommand(uint tick) =>
                new PlanetMotorCommand(tick, Move, false);
        }

        [UnityTest]
        public IEnumerator BasePoseBridge_DeclaresNoRootOrFootOwnership()
        {
            GameObject actor = new GameObject("EAMM authority probe");
            actor.AddComponent<Animator>();
            EAMMBasePoseBridge bridge = actor.AddComponent<EAMMBasePoseBridge>();
            yield return null;

            Assert.That(bridge.OwnsGameplayRoot, Is.False);
            Assert.That(bridge.OwnsFootIk, Is.False);
            Object.Destroy(actor);
        }

        [UnityTest]
        public IEnumerator Presentation_TeardownWithoutAnimationDriver_IsSafe()
        {
            GameObject actor = new GameObject("Presentation teardown probe");
            actor.AddComponent<Animator>();
            HumanoidCharacterPresentation presentation =
                actor.AddComponent<HumanoidCharacterPresentation>();
            yield return null;

            EarthAnimationDriver driver = actor.GetComponent<EarthAnimationDriver>();
            if (driver != null) Object.DestroyImmediate(driver);
            FieldInfo driverField = typeof(HumanoidCharacterPresentation).GetField(
                "animationDriver",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(driverField, Is.Not.Null);
            driverField.SetValue(presentation, null);

            Assert.DoesNotThrow(presentation.ResetMagicIK);
            Object.Destroy(actor);
        }

        [UnityTest]
        public IEnumerator AnimationDriver_WithoutController_RejectsParameterWritesQuietly()
        {
            GameObject actor = new GameObject("Animation driver lifecycle probe");
            Animator animator = actor.AddComponent<Animator>();
            EarthAnimationDriver driver = actor.AddComponent<EarthAnimationDriver>();
            driver.Configure(animator);
            yield return null;

            Assert.That(driver.IsUsable, Is.False);
            Assert.DoesNotThrow(() =>
            {
                driver.SetFloat(Animator.StringToHash("Speed"), 1f);
                driver.SetBool(Animator.StringToHash("Grounded"), true);
                driver.SetInteger(Animator.StringToHash("CastKind"), 0);
            });
            Object.Destroy(actor);
        }

        [UnityTest]
        public IEnumerator ProductionEammGraph_PreservesLiveLocomotionParameters()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool loadedForTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedForTest)
            {
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(ScenePath);
            }

            GameObject player = FindByName(scene, "Planet Character");
            Assert.That(player, Is.Not.Null);
            PlanetMotor motor = player.GetComponent<PlanetMotor>();
            Rigidbody body = player.GetComponent<Rigidbody>();
            PlanetInputReader originalInput = player.GetComponent<PlanetInputReader>();
            HumanoidCharacterPresentation presentation =
                player.GetComponentInChildren<HumanoidCharacterPresentation>(true);
            EAMMBasePoseBridge bridge = player.GetComponentInChildren<EAMMBasePoseBridge>(true);
            EarthAnimationDriver driver = player.GetComponentInChildren<EarthAnimationDriver>(true);
            Assert.That(motor, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);

            ScriptedMotorInput scripted = player.AddComponent<ScriptedMotorInput>();
            motor.ConfigureInputSource(scripted);
            player.GetComponent<EarthCharacterImpactTarget>()?.SuppressImpacts(8f);
            for (int tick = 0; tick < 150; tick++) yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True);
            Assert.That(bridge.HasAnimationGraph, Is.True,
                $"EAMM graph did not initialize: {bridge.InitializationStatus} / {bridge.PoseRejectionReason}");

            Vector3 start = body.position;
            scripted.Move = new float2(0f, 1f);
            for (int tick = 0; tick < 32; tick++) yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(Vector3.Distance(start, body.position), Is.GreaterThan(0.35f));
            Assert.That(presentation.FilteredSpeed, Is.GreaterThan(0.25f));
            Assert.That(driver.GetFloat(SpeedHash), Is.GreaterThan(0.20f),
                "The unified EAMM playable must retain the live locomotion value instead of being overwritten by stale Animator defaults.");

            scripted.Move = float2.zero;
            if (originalInput != null) motor.ConfigureInputSource(originalInput);
            Object.Destroy(scripted);
            if (loadedForTest) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                    if (transforms[index].name == objectName) return transforms[index].gameObject;
            }
            return null;
        }
    }
}
