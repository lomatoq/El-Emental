using Elemental.Authoring.Editor;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCoreLocomotionWiringTests
    {
        [Test]
        public void ProductionSceneUsesOneWorldScaleForGravityMotorAndPlanetSupport()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool loadedByTest = !scene.IsValid() || !scene.isLoaded;
            if (loadedByTest)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                PlanetWorldProfile world = AssetDatabase.LoadAssetAtPath<PlanetWorldProfile>(
                    M2VoxelPlanetSetup.WorldProfilePath);
                GameObject player = FindByName(scene, "Planet Character");
                GameObject bot = FindByName(scene, "Rumble Linebreaker Bot");
                PlanetMotor motor = player != null ? player.GetComponent<PlanetMotor>() : null;
                PlanetMotor botMotor = bot != null ? bot.GetComponent<PlanetMotor>() : null;
                PointPlanetGravitySource gravity = FindInScene<PointPlanetGravitySource>(scene);
                GravityWorldBehaviour gravityWorld = FindInScene<GravityWorldBehaviour>(scene);
                VoxelPlanetEarthSurfaceProvider surface =
                    FindInScene<VoxelPlanetEarthSurfaceProvider>(scene);

                Assert.That(world, Is.Not.Null);
                Assert.That(player, Is.Not.Null);
                Assert.That(bot, Is.Not.Null);
                Assert.That(motor, Is.Not.Null);
                Assert.That(botMotor, Is.Not.Null);
                Assert.That(gravity, Is.Not.Null);
                Assert.That(gravityWorld, Is.Not.Null);
                Assert.That(surface, Is.Not.Null);
                Assert.That(gravity.Radius, Is.EqualTo(world.Radius).Within(0.0001f));
                Assert.That(gravity.SurfaceAcceleration,
                    Is.EqualTo(world.SurfaceGravity).Within(0.0001f));
                float sampledGravity = math.length(gravity.BuildField().Sample(
                    (float3)motor.Body.worldCenterOfMass, 0u).Acceleration);
                Assert.That(sampledGravity, Is.GreaterThan(world.SurfaceGravity * 0.9f),
                    "The character surface must not use the one-metre gravity-source default.");
                Assert.That(motor.FeelProfile, Is.Not.Null);
                Assert.That(motor.JumpSpeed, Is.EqualTo(motor.FeelProfile.JumpSpeed).Within(0.001f));
                Assert.That(motor.UsesTankSteering, Is.True,
                    "Player locomotion must rotate its canonical body instead of strafing legs under a fixed torso.");
                Assert.That(botMotor.UsesTankSteering, Is.True);

                GravityBody playerGravity = player.GetComponent<GravityBody>();
                GravityBody botGravity = bot.GetComponent<GravityBody>();
                Assert.That(playerGravity, Is.Not.Null);
                Assert.That(botGravity, Is.Not.Null);
                Assert.That(playerGravity.GravityWorld, Is.SameAs(gravityWorld));
                Assert.That(botGravity.GravityWorld, Is.SameAs(gravityWorld));
                Assert.That(playerGravity.TargetBody, Is.SameAs(motor.Body));
                Assert.That(botGravity.TargetBody, Is.SameAs(botMotor.Body));
                Assert.That(motor.Body.useGravity, Is.False);
                Assert.That(botMotor.Body.useGravity, Is.False);

                float expectedRise = motor.JumpSpeed * motor.JumpSpeed / (2f * sampledGravity);
                Assert.That(expectedRise, Is.InRange(0.18f, 0.75f),
                    "The generated player must use the compact authored jump envelope under real surface gravity.");
            }
            finally
            {
                if (loadedByTest) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EammLocomotionRetargetOwnsOneCoherentBodyPoseButNotUnknownBones()
        {
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(HumanBodyBones.Hips), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(HumanBodyBones.Chest), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(HumanBodyBones.LeftUpperArm), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(HumanBodyBones.LeftUpperLeg), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(HumanBodyBones.LastBone), Is.Zero);
        }

        [Test]
        public void AuthoredIdleCedesOnlyLowerLegRotationSoHumanoidIkKeepsAKneeMargin()
        {
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.LeftLowerLeg, true), Is.Zero);
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.RightLowerLeg, true), Is.Zero);

            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.LeftLowerLeg, false), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.LeftUpperLeg, true), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.LeftFoot, true), Is.EqualTo(1f));
            Assert.That(EAMMBasePoseBridge.ResolveLocomotionBoneWeight(
                HumanBodyBones.Hips, true), Is.EqualTo(1f));
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T found = roots[index].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
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
