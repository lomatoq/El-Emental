using System;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthInertializationTests
    {
        [Test]
        public void AbruptNinetyDegreePoseStartsContinuousAndConverges()
        {
            quaternion source = quaternion.identity;
            quaternion destination = quaternion.RotateY(math.radians(90f));
            EarthInertializationVectorState state = EarthInertializationMath.ComposeRotation(
                source,
                float3.zero,
                destination,
                float3.zero,
                math.PI,
                30f);

            quaternion first = EarthInertializationMath.ApplyRotation(destination, in state);
            Assert.That(AngleDegrees(first, source), Is.LessThan(0.001f));

            for (int index = 0; index < 60; index++)
                EarthInertializationMath.StepCriticallyDamped(ref state, 0.065f, 1f / 60f);
            quaternion settled = EarthInertializationMath.ApplyRotation(destination, in state);
            Assert.That(AngleDegrees(settled, destination), Is.LessThan(0.1f));
        }

        [Test]
        public void InterruptedInertiaComposesFromRenderedPoseAndVelocity()
        {
            quaternion firstTarget = quaternion.RotateY(math.radians(90f));
            EarthInertializationVectorState first = EarthInertializationMath.ComposeRotation(
                quaternion.identity,
                float3.zero,
                firstTarget,
                float3.zero,
                math.PI,
                30f);
            for (int index = 0; index < 5; index++)
                EarthInertializationMath.StepCriticallyDamped(ref first, 0.08f, 1f / 60f);
            quaternion rendered = EarthInertializationMath.ApplyRotation(firstTarget, in first);

            quaternion secondTarget = quaternion.RotateY(math.radians(-90f));
            EarthInertializationVectorState interrupted =
                EarthInertializationMath.ComposeRotation(
                    rendered,
                    first.Velocity,
                    secondTarget,
                    float3.zero,
                    math.PI,
                    30f);
            quaternion firstInterruptedOutput =
                EarthInertializationMath.ApplyRotation(secondTarget, in interrupted);

            Assert.That(AngleDegrees(firstInterruptedOutput, rendered), Is.LessThan(0.001f));
            Assert.That(math.length(interrupted.Velocity), Is.GreaterThan(0.01f));
        }

        [Test]
        public void QuaternionOffsetUsesShortestPathAcrossOneEightyDegrees()
        {
            quaternion source = quaternion.RotateY(math.radians(179f));
            quaternion destination = quaternion.RotateY(math.radians(-179f));
            EarthInertializationVectorState state = EarthInertializationMath.ComposeRotation(
                source,
                float3.zero,
                destination,
                float3.zero,
                math.PI,
                30f);

            Assert.That(math.degrees(math.length(state.Offset)), Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void DecayIsEquivalentAtThirtySixtyAndOneTwentyFps()
        {
            float3 at30 = SimulatePosition(30);
            float3 at60 = SimulatePosition(60);
            float3 at120 = SimulatePosition(120);

            Assert.That(math.distance(at30, at60), Is.LessThan(0.00002f));
            Assert.That(math.distance(at60, at120), Is.LessThan(0.00002f));
        }

        [Test]
        public void MalformedPoseAndTimingStayFinite()
        {
            var state = new EarthInertializationVectorState
            {
                Offset = new float3(float.NaN, float.PositiveInfinity, 1f),
                Velocity = new float3(float.NegativeInfinity, 2f, float.NaN)
            };
            EarthInertializationMath.StepCriticallyDamped(
                ref state,
                float.NaN,
                float.PositiveInfinity);
            quaternion rotation = EarthInertializationMath.ApplyRotation(
                new quaternion(float.NaN, 0f, 0f, 0f),
                in state);

            Assert.That(EarthInertializationMath.IsFinite(state.Offset), Is.True);
            Assert.That(EarthInertializationMath.IsFinite(state.Velocity), Is.True);
            Assert.That(EarthInertializationMath.IsFinite(rotation), Is.True);
        }

        [Test]
        public void PlantedFootAndRagdollOwnershipExcludeGenericDecay()
        {
            EarthAnimationBoneOwnership leftLeg = EarthAnimationBoneMask.OwnershipFor(
                HumanBodyBones.LeftLowerLeg);
            EarthAnimationBoneOwnership chest = EarthAnimationBoneMask.OwnershipFor(
                HumanBodyBones.Chest);

            Assert.That(EarthAnimationBoneMask.ShouldApplyInertialization(
                leftLeg,
                EarthAnimationBoneOwnership.LeftFootPlant), Is.False);
            Assert.That(EarthAnimationBoneMask.ShouldApplyInertialization(
                chest,
                EarthAnimationBoneOwnership.LeftFootPlant), Is.True);
            Assert.That(EarthAnimationBoneMask.ShouldApplyInertialization(
                chest,
                EarthAnimationBoneOwnership.FullRagdoll), Is.False);
        }

        [Test]
        public void HotMathLoopAllocatesNoManagedMemory()
        {
            EarthInertializationVectorState state = EarthInertializationMath.ComposePosition(
                new float3(0.3f, -0.1f, 0.2f),
                new float3(2f, 0f, -1f),
                float3.zero,
                float3.zero,
                1f,
                10f);
            EarthInertializationMath.StepCriticallyDamped(ref state, 0.075f, 1f / 60f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
                EarthInertializationMath.StepCriticallyDamped(ref state, 0.075f, 1f / 120f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [TestCase(EarthMotionStateId.Fall, EarthMotionStateId.SoftLanding,
            EarthMotionCategory.Airborne, EarthMotionCategory.Landing)]
        [TestCase(EarthMotionStateId.KnockdownRecovery, EarthMotionStateId.Locomotion,
            EarthMotionCategory.RagdollRecovery, EarthMotionCategory.Locomotion)]
        public void DirectorPolicyRequestsInertiaForFirstWaveStateSeams(
            EarthMotionStateId source,
            EarthMotionStateId destination,
            EarthMotionCategory sourceCategory,
            EarthMotionCategory destinationCategory)
        {
            var context = new EarthAnimationTransitionContext(
                source,
                destination,
                sourceCategory,
                destinationCategory,
                EarthAnimationTransitionPriority.LandingContact,
                EarthAnimationTransitionPriority.Idle,
                0.4f,
                0.25f,
                1f,
                0.6f,
                0.1f,
                destinationCategory == EarthMotionCategory.Landing,
                true,
                false,
                true);
            var tuning = new EarthAnimationTransitionTuning(
                0.14f, 0.12f, 0.06f, 0.10f, 0.07f,
                0.12f, 0.16f, 0.12f, 0.08f, false, true);

            EarthAnimationTransitionDecision decision =
                EarthAnimationTransitionPolicy.Resolve(in context, in tuning);
            Assert.That(decision.Kind, Is.EqualTo(EarthAnimationTransitionKind.Inertialized));
            Assert.That(decision.RequestsInertialization, Is.True);
        }

        [Test]
        public void GraphTopologyContainsControllerThenAnimationScriptPlayable()
        {
            CharacterPresentationProfile characterProfile = AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(
                "Assets/Elemental/Content/Profiles/CharacterPresentationProfile.asset");
            Assert.That(characterProfile, Is.Not.Null);
            GameObject instance = UnityEngine.Object.Instantiate(characterProfile.HumanoidPrefab);
            instance.SetActive(false);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                animator.runtimeAnimatorController = characterProfile.AnimatorController;
                animator.avatar = characterProfile.Avatar;
                Type rigBuilderType = Type.GetType(
                    "UnityEngine.Animations.Rigging.RigBuilder, Unity.Animation.Rigging");
                Assert.That(rigBuilderType, Is.Not.Null);
                var legacyRigBuilder = (Behaviour)animator.gameObject.AddComponent(rigBuilderType);
                EarthAnimationGraph graph = animator.gameObject.AddComponent<EarthAnimationGraph>();
                instance.SetActive(true);
                var settings = new EarthAnimationGraphSettings(true, true);

                Assert.That(graph.Configure(animator, in settings), Is.True);
                EarthAnimationGraphDiagnostics diagnostics = graph.Diagnostics;
                Assert.That(diagnostics.GraphValid, Is.True);
                Assert.That(diagnostics.ControllerPlayableValid, Is.True);
                Assert.That(diagnostics.AnimationScriptPlayableValid, Is.True);
                Assert.That(diagnostics.TopologyValid, Is.True);
                Assert.That(diagnostics.LegacyFallbackActive, Is.False);
                Assert.That(legacyRigBuilder.enabled, Is.False,
                    "The external graph must disable the RigBuilder-owned graph.");

                PlayableGraph activeGraph = graph.ControllerPlayable.GetGraph();
                Assert.That(activeGraph.IsValid(), Is.True);
                UnityEngine.Object.DestroyImmediate(graph);
                Assert.That(activeGraph.IsValid(), Is.False,
                    "Explicit EditMode destruction must invalidate every graph handle.");
                Assert.That(legacyRigBuilder.enabled, Is.True,
                    "Disposal must restore legacy RigBuilder ownership without stale graph handles.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static float3 SimulatePosition(int fps)
        {
            var state = new EarthInertializationVectorState
            {
                Offset = new float3(0.42f, -0.13f, 0.08f),
                Velocity = new float3(1.5f, -0.2f, 0.4f)
            };
            float delta = 1f / fps;
            for (int index = 0; index < fps; index++)
                EarthInertializationMath.StepCriticallyDamped(ref state, 0.075f, delta);
            return state.Offset;
        }

        private static float AngleDegrees(quaternion a, quaternion b)
        {
            float dot = math.abs(math.dot(
                EarthInertializationMath.Sanitize(a).value,
                EarthInertializationMath.Sanitize(b).value));
            return math.degrees(2f * math.acos(math.clamp(dot, -1f, 1f)));
        }
    }
}
