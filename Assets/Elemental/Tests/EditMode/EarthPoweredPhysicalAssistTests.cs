using System;
using Elemental.Core.IDs;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPoweredPhysicalAssistTests
    {
        [Test]
        public void SeverityOwnership_LightStaysAgentA_MediumAssist_HeavyExistingRagdoll()
        {
            var assist = new EarthPoweredPhysicalAssist();

            EarthPoweredImpactDecision light = Claim(assist,
                1u, EarthCharacterImpactResponse.Flinch, 0.2f, new float3(1f, 0f, 0f));
            EarthPoweredImpactDecision medium = Claim(assist,
                2u, EarthCharacterImpactResponse.Stagger, 0.6f, new float3(1f, 0f, 0f));
            EarthPoweredImpactDecision heavy = Claim(assist,
                3u, EarthCharacterImpactResponse.Knockout, 1f, new float3(1f, 0f, 0f));

            Assert.That(light.Owner, Is.EqualTo(EarthPoweredImpactOwner.AgentAInertialResponse));
            Assert.That(medium.Owner, Is.EqualTo(EarthPoweredImpactOwner.PoweredPhysicalAssist));
            Assert.That(heavy.Owner, Is.EqualTo(EarthPoweredImpactOwner.ExistingFullRagdoll));
            Assert.That(light.EmitsImpulse || medium.EmitsImpulse || heavy.EmitsImpulse, Is.False);
            Assert.That(light.RequestsRagdoll || medium.RequestsRagdoll || heavy.RequestsRagdoll, Is.False);
        }

        [Test]
        public void MediumHit_PreservesStableSupportAndLegDriveOwnership()
        {
            var canonical = new CharacterPhysicalController(
                new ActorId(1u), CharacterPhysicalTuning.Default);
            Assert.That(canonical.TryRequestPoweredAssist(false), Is.True);
            Assert.That(canonical.Mode, Is.EqualTo(CharacterPhysicalMode.PhysicalAssist));

            var assist = new EarthPoweredPhysicalAssist();
            Claim(assist,
                7u, EarthCharacterImpactResponse.Stagger, 0.65f, new float3(1f, 0f, 0f));
            EarthPoweredAssistInput input = SupportedInput(
                CharacterPhysicalMode.PhysicalAssist,
                new float3(0f, 1f, 0f),
                1f / 60f);
            EarthPoweredAssistOutput output = assist.Step(in input);
            EarthMuscleProfile profile = EarthMuscleProfiles.Resolve(output.Profile);

            Assert.That(output.PreservesFeet, Is.True);
            Assert.That(output.Behaviours.HasFlag(EarthPoweredBehaviour.MaintainBalance), Is.True);
            Assert.That(profile.Leg.DriveWeight, Is.Zero,
                "Medium powered assist must not fight authored foot animation or foot IK.");
            Assert.That(profile.Leg.TorqueCap, Is.Zero);
        }

        [Test]
        public void EveryCanonicalProfileLeavesLegDriveFullyUnowned()
        {
            foreach (EarthMuscleProfileId id in Enum.GetValues(typeof(EarthMuscleProfileId)))
            {
                EarthMuscleRegionTuning leg = EarthMuscleProfiles.Resolve(id).Leg;
                Assert.That(leg.Frequency, Is.Zero, $"{id} frequency");
                Assert.That(leg.Damping, Is.Zero, $"{id} damping");
                Assert.That(leg.TorqueCap, Is.Zero, $"{id} torque");
                Assert.That(leg.DriveWeight, Is.Zero, $"{id} drive");
                Assert.That(leg.TransferWeight, Is.Zero, $"{id} transfer");
            }
        }

        [Test]
        public void JointDefaultsUnassignedAndFailsLoudWithDriveDisabled()
        {
            var root = new GameObject("Unassigned Powered Joint");
            var target = new GameObject("Joint Target");
            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                ConfigurableJoint configurable = root.AddComponent<ConfigurableJoint>();
                ActiveRagdollJoint joint = root.AddComponent<ActiveRagdollJoint>();
                joint.Configure(body, configurable, target.transform, 100f, 10f, 50f, 30f);
                Assert.That(joint.HasConfiguredBodyRegion, Is.False);
                Assert.That(joint.BodyRegion, Is.EqualTo(EarthBodyRegion.Unassigned));

                EarthMuscleRegionTuning chest = EarthMuscleProfiles.Resolve(
                    EarthMuscleProfileId.Reactive).Chest;
                LogAssert.Expect(
                    LogType.Error,
                    "ActiveRagdollJoint on 'Unassigned Powered Joint' disabled powered assist because its body region is unassigned.");
                joint.ApplyPoweredPose(in chest, 1f, 1f / 60f);
                Assert.That(configurable.slerpDrive.maximumForce, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OffsetStanceHullIncludesLongitudinalAndLateralFootExtent()
        {
            EarthSupportPolygon polygon = EarthSupportPolygon.FromPlantedFeet(
                new float3(-0.32f, 0f, -0.38f),
                new float3(0.28f, 0f, 0.42f),
                true,
                true,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                0.18f,
                0.08f);

            Assert.That(polygon.IsValid, Is.True);
            Assert.That(polygon.Count, Is.GreaterThanOrEqualTo(4));
            EarthBalanceDecision insideLongitudinalReach = EarthSupportPolygonSolver.Evaluate(
                new float3(0.20f, 1f, 0.36f),
                new float3(0f, 1f, 0f),
                in polygon);
            Assert.That(insideLongitudinalReach.IsOutside, Is.False,
                "The stance hull must retain the planted feet's longitudinal offset.");
        }

        [Test]
        public void SinglePlantedFootExcludesSwingFootFromSupportHull()
        {
            float3 left = new float3(-0.25f, 0f, -0.1f);
            float3 swingRight = new float3(0.8f, 0f, 0.5f);
            EarthSupportPolygon polygon = EarthSupportPolygon.FromPlantedFeet(
                left,
                swingRight,
                true,
                false,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                0.18f,
                0.08f);

            Assert.That(polygon.IsValid, Is.True);
            Assert.That(polygon.Count, Is.EqualTo(4));
            Assert.That(EarthSupportPolygonSolver.Evaluate(
                left + new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                in polygon).IsOutside, Is.False);
            Assert.That(EarthSupportPolygonSolver.Evaluate(
                swingRight + new float3(0f, 1f, 0f),
                new float3(0f, 1f, 0f),
                in polygon).IsOutside, Is.True,
                "A swing foot must never enlarge the support polygon.");
        }

        [Test]
        public void CenterOfMassOutsideSupportPolygon_RequestsOneAuthoredStep()
        {
            var assist = new EarthPoweredPhysicalAssist();
            Claim(assist,
                11u, EarthCharacterImpactResponse.Stagger, 0.7f, new float3(1f, 0f, 0f));
            EarthPoweredAssistInput input = SupportedInput(
                CharacterPhysicalMode.Stagger,
                new float3(0.9f, 1f, 0f),
                1f / 60f);

            EarthPoweredAssistOutput first = assist.Step(in input);
            EarthPoweredAssistOutput second = assist.Step(in input);

            Assert.That(first.Balance.IsOutside, Is.True);
            Assert.That(first.Behaviours.HasFlag(EarthPoweredBehaviour.StaggerStep), Is.True);
            Assert.That(first.Action.Kind, Is.EqualTo(EarthPhysicalActionKind.AuthoredRecoveryStep));
            Assert.That(first.Action.Foot, Is.Not.EqualTo(EarthRecoveryFoot.None));
            Assert.That(first.EmitAction, Is.True);
            Assert.That(second.EmitAction, Is.False,
                "One accepted response may emit only one authored-step request.");
        }

        [Test]
        public void UnreachableBrace_IsRejectedWithoutPelvisForceCommand()
        {
            var assist = new EarthPoweredPhysicalAssist();
            Claim(assist,
                21u, EarthCharacterImpactResponse.Stagger, 0.6f, new float3(0f, 0f, 1f));
            EarthSupportPolygon polygon = Polygon();
            var unreachable = new EarthPhysicalSurfaceProbe(
                EarthSemanticSurfaceKind.Braceable,
                new float3(0f, 1f, -2f),
                new float3(0f, 0f, 1f),
                EarthPoweredPhysicalAssist.MaximumSemanticReach + 0.01f,
                true);
            EarthPhysicalSurfaceProbe none = default;
            var input = new EarthPoweredAssistInput(
                1f / 60f,
                CharacterPhysicalMode.PhysicalAssist,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                float3.zero,
                true,
                true,
                in polygon,
                in unreachable,
                in none,
                in none);

            EarthPoweredAssistOutput output = assist.Step(in input);

            Assert.That(output.Behaviours.HasFlag(
                EarthPoweredBehaviour.BraceAgainstSurface), Is.False);
            Assert.That(output.Action.IsValid, Is.False);
        }

        [Test]
        public void FallProtect_UsesBoundedSemanticProbeAndNeverEmitsImpulse()
        {
            var assist = new EarthPoweredPhysicalAssist();
            EarthPoweredImpactDecision decision = Claim(assist,
                29u, EarthCharacterImpactResponse.Stagger, 0.9f, new float3(0f, -1f, 0f));
            EarthSupportPolygon polygon = default;
            EarthPhysicalSurfaceProbe none = default;
            var arrest = new EarthPhysicalSurfaceProbe(
                EarthSemanticSurfaceKind.FallArrest,
                new float3(0f, 0f, 0f),
                new float3(0f, 1f, 0f),
                0.8f,
                true);
            var input = new EarthPoweredAssistInput(
                1f / 60f,
                CharacterPhysicalMode.PhysicalAssist,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 2f, 0f),
                new float3(0f, -4f, 0f),
                false,
                false,
                in polygon,
                in none,
                in none,
                in arrest);

            EarthPoweredAssistOutput output = assist.Step(in input);

            Assert.That(output.Profile, Is.EqualTo(EarthMuscleProfileId.FallProtect));
            Assert.That(output.Behaviours.HasFlag(EarthPoweredBehaviour.ProtectHead), Is.True);
            Assert.That(output.Behaviours.HasFlag(EarthPoweredBehaviour.FallArrest), Is.True);
            Assert.That(output.Action.Kind, Is.EqualTo(EarthPhysicalActionKind.FallArrest));
            Assert.That(decision.EmitsImpulse, Is.False);
        }

        [Test]
        public void ReachableBraceAndSupport_EmitOnlySemanticRequests()
        {
            var braceAssist = new EarthPoweredPhysicalAssist();
            Claim(braceAssist,
                30u, EarthCharacterImpactResponse.Stagger, 0.7f, new float3(0f, 0f, 1f));
            EarthSupportPolygon polygon = Polygon();
            EarthPhysicalSurfaceProbe none = default;
            var brace = new EarthPhysicalSurfaceProbe(
                EarthSemanticSurfaceKind.Braceable,
                new float3(0f, 1f, -0.7f),
                new float3(0f, 0f, 1f),
                0.7f,
                true);
            var braceInput = new EarthPoweredAssistInput(
                1f / 60f,
                CharacterPhysicalMode.PhysicalAssist,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 0f),
                float3.zero,
                true,
                true,
                in polygon,
                in brace,
                in none,
                in none);
            EarthPoweredAssistOutput braceOutput = braceAssist.Step(in braceInput);
            Assert.That(braceOutput.Behaviours.HasFlag(
                EarthPoweredBehaviour.BraceAgainstSurface), Is.True);
            Assert.That(braceOutput.Action.Kind,
                Is.EqualTo(EarthPhysicalActionKind.BraceAgainstSurface));

            var reachAssist = new EarthPoweredPhysicalAssist();
            Claim(reachAssist,
                31u, EarthCharacterImpactResponse.Stagger, 0.7f, new float3(1f, 0f, 0f));
            var reach = new EarthPhysicalSurfaceProbe(
                EarthSemanticSurfaceKind.ReachableSupport,
                new float3(0.8f, 1f, 0f),
                new float3(-1f, 0f, 0f),
                0.8f,
                true);
            EarthSupportPolygon noPolygon = default;
            var reachInput = new EarthPoweredAssistInput(
                1f / 60f,
                CharacterPhysicalMode.PhysicalAssist,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 2f, 0f),
                float3.zero,
                false,
                false,
                in noPolygon,
                in none,
                in reach,
                in none);
            EarthPoweredAssistOutput reachOutput = reachAssist.Step(in reachInput);
            Assert.That(reachOutput.Behaviours.HasFlag(
                EarthPoweredBehaviour.ReachForSupport), Is.True);
            Assert.That(reachOutput.Action.Kind,
                Is.EqualTo(EarthPhysicalActionKind.ReachForSupport));
        }

        [Test]
        public void ResponseIdentity_DeduplicatesAndEvictsAtFixedCapacity()
        {
            var assist = new EarthPoweredPhysicalAssist();
            for (uint id = 1u; id <= EarthPoweredPhysicalAssist.ResponseHistoryCapacity; id++)
                Assert.That(Claim(assist,
                    id,
                    EarthCharacterImpactResponse.Stagger,
                    0.5f,
                    new float3(1f, 0f, 0f)).Accepted, Is.True);

            EarthPoweredImpactDecision duplicate = Claim(assist,
                1u, EarthCharacterImpactResponse.Stagger, 0.5f, new float3(1f, 0f, 0f));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(assist.AcceptedResponseCount,
                Is.EqualTo(EarthPoweredPhysicalAssist.ResponseHistoryCapacity));

            Assert.That(Claim(assist,
                17u,
                EarthCharacterImpactResponse.Stagger,
                0.5f,
                new float3(1f, 0f, 0f)).Accepted, Is.True);
            Assert.That(Claim(assist,
                1u,
                EarthCharacterImpactResponse.Stagger,
                0.5f,
                new float3(1f, 0f, 0f)).Accepted, Is.True,
                "The oldest response ID must be reusable after fixed-ring eviction.");
        }

        [Test]
        public void MediumClaimRequiresLiveEligibilityAndControllerAcceptance()
        {
            Assert.That(EarthPoweredAssistEligibility.Evaluate(
                CharacterPhysicalMode.AnimatedMotor,
                false,
                true,
                true,
                true,
                true), Is.EqualTo(EarthPoweredAssistRejection.UnstableSupport));
            Assert.That(EarthPoweredAssistEligibility.Evaluate(
                CharacterPhysicalMode.AnimatedMotor,
                true,
                false,
                true,
                true,
                true), Is.EqualTo(EarthPoweredAssistRejection.MissingFeet));
            Assert.That(EarthPoweredAssistEligibility.Evaluate(
                CharacterPhysicalMode.AnimatedMotor,
                true,
                true,
                false,
                false,
                false), Is.EqualTo(EarthPoweredAssistRejection.NoPlantedFoot));
            Assert.That(EarthPoweredAssistEligibility.Evaluate(
                CharacterPhysicalMode.FullRagdoll,
                true,
                true,
                true,
                true,
                true), Is.EqualTo(EarthPoweredAssistRejection.CanonicalModeRejected));
            Assert.That(EarthPoweredAssistEligibility.Evaluate(
                CharacterPhysicalMode.Recovery,
                true,
                true,
                true,
                true,
                true), Is.EqualTo(EarthPoweredAssistRejection.CanonicalModeRejected));

            var assist = new EarthPoweredPhysicalAssist();
            AssertRejectedClaimDoesNotConsume(
                assist, 0xD001u, EarthPoweredAssistRejection.UnstableSupport);
            AssertRejectedClaimDoesNotConsume(
                assist, 0xD002u, EarthPoweredAssistRejection.MissingFeet);
            AssertRejectedClaimDoesNotConsume(
                assist, 0xD003u, EarthPoweredAssistRejection.NoPlantedFoot);
            AssertRejectedClaimDoesNotConsume(
                assist, 0xD004u, EarthPoweredAssistRejection.CanonicalModeRejected);
            AssertRejectedClaimDoesNotConsume(
                assist, 0xD005u, EarthPoweredAssistRejection.ControllerRejected);

            EarthPoweredImpactDecision accepted = Claim(
                assist,
                0xD001u,
                EarthCharacterImpactResponse.Stagger,
                0.7f,
                new float3(1f, 0f, 0f));
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.Owner,
                Is.EqualTo(EarthPoweredImpactOwner.PoweredPhysicalAssist));
            Assert.That(assist.IsResponseKnown(0xD001u), Is.True);
        }

        [Test]
        public void DistinctHeavyHitHasOneExistingRagdollOwnerAndDuplicateHasNone()
        {
            var assist = new EarthPoweredPhysicalAssist();
            EarthPoweredImpactDecision first = Claim(assist,
                40u, EarthCharacterImpactResponse.Knockout, 1f, new float3(0f, 1f, 0f));
            EarthPoweredImpactDecision duplicate = Claim(assist,
                40u, EarthCharacterImpactResponse.Knockout, 1f, new float3(0f, 1f, 0f));

            Assert.That(first.Owner, Is.EqualTo(EarthPoweredImpactOwner.ExistingFullRagdoll));
            Assert.That(first.RequestsRagdoll, Is.False);
            Assert.That(first.EmitsImpulse, Is.False);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.Owner, Is.EqualTo(EarthPoweredImpactOwner.None));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void ResponseRecovery_IsTimeNormalizedAcrossFixedRates(int rate)
        {
            var assist = new EarthPoweredPhysicalAssist();
            Claim(assist,
                51u, EarthCharacterImpactResponse.Stagger, 0.8f, new float3(1f, 0f, 0f));
            float dt = 1f / rate;
            EarthPoweredAssistOutput output = default;
            int ticks = rate * 3 / 10;
            for (int tick = 0; tick < ticks; tick++)
            {
                EarthPoweredAssistInput input = SupportedInput(
                    CharacterPhysicalMode.PhysicalAssist,
                    new float3(0f, 1f, 0f),
                    dt);
                output = assist.Step(in input);
            }

            Assert.That(output.ResponseWeight, Is.EqualTo(0.375f).Within(0.001f));
            Assert.That(output.Profile, Is.EqualTo(EarthMuscleProfileId.Reactive));
        }

        [Test]
        public void MuscleRecovery_IsEquivalentAtThirtySixtyAndOneTwentyHertz()
        {
            float atThirty = SimulateDriveRecovery(30);
            float atSixty = SimulateDriveRecovery(60);
            float atOneTwenty = SimulateDriveRecovery(120);

            Assert.That(atThirty, Is.EqualTo(atSixty).Within(0.0001f));
            Assert.That(atSixty, Is.EqualTo(atOneTwenty).Within(0.0001f));
            Assert.That(atOneTwenty, Is.InRange(0.99f, 1f));
        }

        [Test]
        public void EveryMuscleProfileHasFiniteBoundedRegionalEnergy()
        {
            foreach (EarthMuscleProfileId id in Enum.GetValues(typeof(EarthMuscleProfileId)))
            {
                EarthMuscleProfile profile = EarthMuscleProfiles.Resolve(id);
                foreach (EarthBodyRegion region in Enum.GetValues(typeof(EarthBodyRegion)))
                {
                    EarthMuscleRegionTuning tuning = profile.For(region);
                    float energy = EarthMuscleProfiles.EstimateBoundedJointEnergy(
                        10000f,
                        1000f,
                        in tuning);
                    float maximum = tuning.TorqueCap *
                                    math.radians(tuning.AngularLimitDegrees) *
                                    tuning.TransferWeight;
                    Assert.That(float.IsFinite(energy), Is.True, $"{id}/{region}");
                    Assert.That(energy, Is.InRange(0f, maximum + 0.001f), $"{id}/{region}");
                }
            }
        }

        [Test]
        public void CustomProfileSet_RejectsMissingAndDuplicateIds()
        {
            var profile = ScriptableObject.CreateInstance<EarthPhysicalAnimationProfile>();
            try
            {
                Assert.That(profile.UsePoweredPhysicalAssist, Is.False,
                    "Wave P2 must remain default-off until Director integration is validated.");
                EarthMuscleProfile reactive = EarthMuscleProfiles.Resolve(
                    EarthMuscleProfileId.Reactive);
                var authored = new EarthMuscleProfileAuthoring(in reactive);
                profile.ConfigurePoweredPhysicalAssist(true, new[] { authored });
                Assert.Throws<InvalidOperationException>(() =>
                    profile.ResolveMuscleProfile(EarthMuscleProfileId.Stable));

                profile.ConfigurePoweredPhysicalAssist(true, new[] { authored, authored });
                Assert.Throws<InvalidOperationException>(() =>
                    profile.ResolveMuscleProfile(EarthMuscleProfileId.Reactive));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CanonicalModeOwnsAssistAndRejectsRecoveryInterruption()
        {
            var controller = new CharacterPhysicalController(
                new ActorId(1u), CharacterPhysicalTuning.Default);
            Assert.That(controller.TryRequestPoweredAssist(true), Is.True);
            Assert.That(controller.Mode, Is.EqualTo(CharacterPhysicalMode.Stagger));
            controller.ForceFullRagdoll();
            Assert.That(controller.TryRequestPoweredAssist(false), Is.False);
            Assert.That(controller.Mode, Is.EqualTo(CharacterPhysicalMode.FullRagdoll));
            Assert.That(controller.TryForceRecovery(RecoveryCandidate.FaceUp), Is.True);
            Assert.That(controller.TryRequestPoweredAssist(true), Is.False);
            Assert.That(controller.Mode, Is.EqualTo(CharacterPhysicalMode.Recovery));

            var ownership = new EarthPhysicalAnimationCoordinator();
            Assert.That(ownership.IsConsistentWith(CharacterPhysicalMode.AnimatedMotor), Is.True);
            Assert.That(ownership.IsConsistentWith(CharacterPhysicalMode.PhysicalAssist), Is.True);
            Assert.That(ownership.IsConsistentWith(CharacterPhysicalMode.Stagger), Is.True);
        }

        [Test]
        public void HotLoopStep_AllocatesZeroManagedBytes()
        {
            var assist = new EarthPoweredPhysicalAssist();
            Claim(assist,
                63u, EarthCharacterImpactResponse.Stagger, 0.7f, new float3(1f, 0f, 0f));
            EarthPoweredAssistInput input = SupportedInput(
                CharacterPhysicalMode.Stagger,
                new float3(0.8f, 1f, 0f),
                1f / 60f);
            for (int index = 0; index < 32; index++) assist.Step(in input);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++) assist.Step(in input);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.EqualTo(0L));
        }

        private static EarthPoweredAssistInput SupportedInput(
            CharacterPhysicalMode mode,
            float3 centerOfMass,
            float deltaTime)
        {
            EarthSupportPolygon polygon = Polygon();
            EarthPhysicalSurfaceProbe none = default;
            return new EarthPoweredAssistInput(
                deltaTime,
                mode,
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                centerOfMass,
                float3.zero,
                true,
                true,
                in polygon,
                in none,
                in none,
                in none);
        }

        private static EarthSupportPolygon Polygon() => EarthSupportPolygon.FromFeet(
            new float3(-0.2f, 0f, 0f),
            new float3(0.2f, 0f, 0f),
            new float3(0f, 1f, 0f),
            new float3(0f, 0f, 1f),
            0.18f,
            0.08f);

        private static EarthPoweredImpactDecision Claim(
            EarthPoweredPhysicalAssist assist,
            uint responseId,
            EarthCharacterImpactResponse response,
            float intensity,
            float3 direction) => assist.RouteAcceptedResponse(
                responseId,
                response,
                intensity,
                direction,
                true,
                EarthPoweredAssistRejection.None);

        private static void AssertRejectedClaimDoesNotConsume(
            EarthPoweredPhysicalAssist assist,
            uint responseId,
            EarthPoweredAssistRejection rejection)
        {
            EarthPoweredImpactDecision fallback = assist.RouteAcceptedResponse(
                responseId,
                EarthCharacterImpactResponse.Stagger,
                0.7f,
                new float3(1f, 0f, 0f),
                false,
                rejection);
            Assert.That(fallback.Accepted, Is.False);
            Assert.That(fallback.FallsBackToAgentA, Is.True);
            Assert.That(fallback.Rejection, Is.EqualTo(rejection));
            Assert.That(assist.IsResponseKnown(responseId), Is.False,
                "Rejected ownership must not consume the canonical response ID.");
        }

        private static float SimulateDriveRecovery(int rate)
        {
            float value = 0f;
            float deltaTime = 1f / rate;
            for (int tick = 0; tick < rate; tick++)
                value = EarthMuscleProfiles.StepDriveWeight(
                    value, 1f, 5f, deltaTime);
            return value;
        }
    }
}
