using System;
using Elemental.Core.IDs;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthPoweredPhysicalAssistTests
    {
        [Test]
        public void SeverityOwnership_LightStaysAgentA_MediumAssist_HeavyExistingRagdoll()
        {
            var assist = new EarthPoweredPhysicalAssist();

            EarthPoweredImpactDecision light = assist.RouteAcceptedResponse(
                1u, EarthCharacterImpactResponse.Flinch, 0.2f, new float3(1f, 0f, 0f));
            EarthPoweredImpactDecision medium = assist.RouteAcceptedResponse(
                2u, EarthCharacterImpactResponse.Stagger, 0.6f, new float3(1f, 0f, 0f));
            EarthPoweredImpactDecision heavy = assist.RouteAcceptedResponse(
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
            assist.RouteAcceptedResponse(
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
        public void CenterOfMassOutsideSupportPolygon_RequestsOneAuthoredStep()
        {
            var assist = new EarthPoweredPhysicalAssist();
            assist.RouteAcceptedResponse(
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
            assist.RouteAcceptedResponse(
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
            EarthPoweredImpactDecision decision = assist.RouteAcceptedResponse(
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
            braceAssist.RouteAcceptedResponse(
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
            reachAssist.RouteAcceptedResponse(
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
                Assert.That(assist.RouteAcceptedResponse(
                    id,
                    EarthCharacterImpactResponse.Stagger,
                    0.5f,
                    new float3(1f, 0f, 0f)).Accepted, Is.True);

            EarthPoweredImpactDecision duplicate = assist.RouteAcceptedResponse(
                1u, EarthCharacterImpactResponse.Stagger, 0.5f, new float3(1f, 0f, 0f));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(assist.AcceptedResponseCount,
                Is.EqualTo(EarthPoweredPhysicalAssist.ResponseHistoryCapacity));

            Assert.That(assist.RouteAcceptedResponse(
                17u,
                EarthCharacterImpactResponse.Stagger,
                0.5f,
                new float3(1f, 0f, 0f)).Accepted, Is.True);
            Assert.That(assist.RouteAcceptedResponse(
                1u,
                EarthCharacterImpactResponse.Stagger,
                0.5f,
                new float3(1f, 0f, 0f)).Accepted, Is.True,
                "The oldest response ID must be reusable after fixed-ring eviction.");
        }

        [Test]
        public void DistinctHeavyHitHasOneExistingRagdollOwnerAndDuplicateHasNone()
        {
            var assist = new EarthPoweredPhysicalAssist();
            EarthPoweredImpactDecision first = assist.RouteAcceptedResponse(
                40u, EarthCharacterImpactResponse.Knockout, 1f, new float3(0f, 1f, 0f));
            EarthPoweredImpactDecision duplicate = assist.RouteAcceptedResponse(
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
            assist.RouteAcceptedResponse(
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
            assist.RouteAcceptedResponse(
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
