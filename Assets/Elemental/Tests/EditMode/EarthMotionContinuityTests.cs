using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMotionContinuityTests
    {
        [Test]
        public void MagicMotionTime_UsesStableAuthoredPhaseWindows()
        {
            Assert.That(EarthHumanoidMotionResolver.ResolveMotionTime(EarthCastPhase.Acquire), Is.EqualTo(0.06f));
            Assert.That(EarthHumanoidMotionResolver.ResolveMotionTime(EarthCastPhase.Strike), Is.EqualTo(0.52f));
            Assert.That(EarthHumanoidMotionResolver.ResolveMotionTime(EarthCastPhase.Sustain), Is.EqualTo(0.68f));
            Assert.That(EarthHumanoidMotionResolver.ResolveMotionTime(EarthCastPhase.Recover), Is.EqualTo(0.88f));
        }

        [Test]
        public void MaskedUpperBodyRecoveryContinuesWhileLocomotionMoves()
        {
            Assert.That(EarthHumanoidMotionResolver.ShouldInterruptRecovery(EarthCastPhase.Recover, 0.16f),
                Is.False);
            Assert.That(EarthHumanoidMotionResolver.ShouldInterruptRecovery(EarthCastPhase.Recover, 1f),
                Is.False);
            Assert.That(EarthHumanoidMotionResolver.ShouldInterruptRecovery(EarthCastPhase.Strike, 1f),
                Is.False);
        }

        [Test]
        public void EveryShippingElementAbilityMapsToAVisibleSemanticPose()
        {
            (ElementId element, ushort ability, EarthHumanoidPoseSlot slot)[] cases =
            {
                (ElementId.Earth, 1, EarthHumanoidPoseSlot.RaiseWall),
                (ElementId.Earth, 2, EarthHumanoidPoseSlot.PullStone),
                (ElementId.Earth, 3, EarthHumanoidPoseSlot.HeavyThrow),
                (ElementId.Earth, 4, EarthHumanoidPoseSlot.RaisePlatform),
                (ElementId.Earth, 5, EarthHumanoidPoseSlot.VectorPush),
                (ElementId.Earth, 6, EarthHumanoidPoseSlot.Pillar),
                (ElementId.Air, 101, EarthHumanoidPoseSlot.VectorPush),
                (ElementId.Air, 102, EarthHumanoidPoseSlot.GravityRepair),
                (ElementId.Air, 103, EarthHumanoidPoseSlot.Pillar),
                (ElementId.Air, 104, EarthHumanoidPoseSlot.GenericCast),
                (ElementId.Fire, 201, EarthHumanoidPoseSlot.VectorPush),
                (ElementId.Fire, 202, EarthHumanoidPoseSlot.GravityRepair),
                (ElementId.Water, 301, EarthHumanoidPoseSlot.PullStone),
                (ElementId.Water, 302, EarthHumanoidPoseSlot.VectorPush),
                (ElementId.Water, 303, EarthHumanoidPoseSlot.RaisePlatform),
                (ElementId.Water, 304, EarthHumanoidPoseSlot.WaveResonance)
            };
            foreach ((ElementId element, ushort ability, EarthHumanoidPoseSlot expected) in cases)
            {
                EarthTechniqueId technique = MagicPresentationSemanticResolver.ResolveTechnique(
                    element,
                    new AbilityId(ability));
                Assert.That(EarthHumanoidMotionResolver.Resolve(technique), Is.EqualTo(expected),
                    $"{element}/{ability}");
            }
        }

        [Test]
        public void ArmorFormationFlightDoesNotSnapAndConvergesWithoutOvershoot()
        {
            float3 target = new float3(8f, 2f, -1f);
            EarthArmorFlightSample first = EarthArmorFormationSolver.StepFlight(
                float3.zero, float3.zero, target, 0.24f, 18f, 0.02f);
            Assert.That(math.length(first.Position), Is.GreaterThan(0f));
            Assert.That(math.distance(first.Position, target), Is.GreaterThan(0.5f),
                "A wheel phase change must remain visible as travel, not one-tick teleportation.");

            EarthArmorFlightSample sample = first;
            float previousDistance = math.distance(sample.Position, target);
            for (int step = 0; step < 180; step++)
            {
                sample = EarthArmorFormationSolver.StepFlight(
                    sample.Position, sample.Velocity, target, 0.24f, 18f, 0.02f);
                float distance = math.distance(sample.Position, target);
                Assert.That(distance, Is.LessThanOrEqualTo(previousDistance + 0.0001f));
                previousDistance = distance;
            }
            Assert.That(math.distance(sample.Position, target), Is.LessThan(0.01f));
        }

        [Test]
        public void SemanticEarthTechniquesResolveToDedicatedCuratedSlots()
        {
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.RaiseWall),
                Is.EqualTo(EarthHumanoidPoseSlot.RaiseWall));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.RaisePlatform),
                Is.EqualTo(EarthHumanoidPoseSlot.RaisePlatform));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.ThrowStone),
                Is.EqualTo(EarthHumanoidPoseSlot.HeavyThrow));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.VectorPush),
                Is.EqualTo(EarthHumanoidPoseSlot.VectorPush));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.ArmorBarrage),
                Is.EqualTo(EarthHumanoidPoseSlot.ArmorBarrage));
            Assert.That(EarthHumanoidMotionResolver.Resolve(EarthTechniqueId.Surf),
                Is.EqualTo(EarthHumanoidPoseSlot.None),
                "Surf owns the crouched base layer and must not blend a standing upper-body cast over it.");
        }

        [Test]
        public void EmergingPlatformCannotBePluckedIntoItsFinalPose()
        {
            Assert.That(EarthEmergingStructureInteractionPolicy.AllowsPluck(0f, false), Is.False);
            Assert.That(EarthEmergingStructureInteractionPolicy.AllowsPluck(0.74f, false), Is.False);
            Assert.That(EarthEmergingStructureInteractionPolicy.AllowsPluck(1f, false), Is.True);
            Assert.That(EarthEmergingStructureInteractionPolicy.AllowsPluck(0.2f, true), Is.True);
        }

        [Test]
        public void WaveCellFinishesRetreatFullyBelowItsSourceSurface()
        {
            EarthPillarWaveMotionSample retreat = EarthPillarWaveSolver.EvaluateMotion(
                0.36f + 0.08f + 0.46f * 0.999f,
                0.36f,
                0.08f,
                0.46f);
            float offset = EarthPillarWaveSolver.ResolveCellBaseOffset(1.25f, 1.45f, in retreat);
            Assert.That(retreat.Complete, Is.False);
            Assert.That(offset, Is.LessThan(-1.4f),
                "The final rendered retreat frame must be deep underground before pooling.");
        }
    }
}
