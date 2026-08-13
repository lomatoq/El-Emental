using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthTechniqueContractTests
    {
        [Test]
        public void SixCoreTechniquesResolveWithoutHotbarSelection()
        {
            AssertTechnique(EarthTechniqueKind.Grip, Context(
                EarthTechniqueGesture.Tap, EarthTechniqueModifierFlags.Primary,
                physicalTarget: true));
            AssertTechnique(EarthTechniqueKind.Wall, Context(
                EarthTechniqueGesture.Line, EarthTechniqueModifierFlags.Primary,
                terrain: true));
            AssertTechnique(EarthTechniqueKind.Platform, Context(
                EarthTechniqueGesture.ClosedRegion, EarthTechniqueModifierFlags.Primary,
                terrain: true));
            AssertTechnique(EarthTechniqueKind.Pillar, Context(
                EarthTechniqueGesture.Tap,
                EarthTechniqueModifierFlags.Force | EarthTechniqueModifierFlags.Jump,
                terrain: true));
            AssertTechnique(EarthTechniqueKind.GroundWave, Context(
                EarthTechniqueGesture.Sweep,
                EarthTechniqueModifierFlags.Primary | EarthTechniqueModifierFlags.Force,
                terrain: true));
            AssertTechnique(EarthTechniqueKind.Repair, Context(
                EarthTechniqueGesture.None, EarthTechniqueModifierFlags.Field,
                broken: true));
        }

        [Test]
        public void RejectionsAreExplicitAndStable()
        {
            EarthTechniqueResolution airPillar = EarthTechniqueRouter.Resolve(Context(
                EarthTechniqueGesture.Tap,
                EarthTechniqueModifierFlags.Force | EarthTechniqueModifierFlags.Jump,
                terrain: true,
                grounded: false));
            Assert.That(airPillar.Rejection, Is.EqualTo(EarthTechniqueRejectReason.NotGrounded));

            EarthTechniqueResolution unknownRepair = EarthTechniqueRouter.Resolve(Context(
                EarthTechniqueGesture.None,
                EarthTechniqueModifierFlags.Field,
                broken: true,
                provenance: false));
            Assert.That(unknownRepair.Rejection, Is.EqualTo(EarthTechniqueRejectReason.MissingProvenance));

            EarthTechniqueResolution overMass = EarthTechniqueRouter.Resolve(Context(
                EarthTechniqueGesture.Tap,
                EarthTechniqueModifierFlags.Primary,
                physicalTarget: true,
                overMass: true));
            Assert.That(overMass.Rejection, Is.EqualTo(EarthTechniqueRejectReason.OverMass));
        }

        [Test]
        public void ParametersAndPureCommandRoundTripDeterministically()
        {
            uint packed = EarthTechniqueParameterCodec.Pack(0.713f, 0.287f);
            Assert.That(EarthTechniqueParameterCodec.UnpackPrimary(packed), Is.EqualTo(0.713f).Within(0.00002f));
            Assert.That(EarthTechniqueParameterCodec.UnpackSecondary(packed), Is.EqualTo(0.287f).Within(0.00002f));

            var command = new EarthTechniqueCommand(
                42u, 7u, EarthTechniqueKind.Wall, 19u, 3,
                new float3(1f, 2f, 3f), new float3(10f, 0f, 0f),
                0.713f, 0.287f, EarthTechniqueModifierFlags.Primary, 123u, 456u);
            Assert.That(command.Tick, Is.EqualTo(42u));
            Assert.That(command.Technique, Is.EqualTo(EarthTechniqueKind.Wall));
            Assert.That(command.Primary01, Is.EqualTo(0.713f).Within(0.00002f));
            Assert.That(command.Secondary01, Is.EqualTo(0.287f).Within(0.00002f));
            Assert.That(math.length(command.Direction), Is.EqualTo(1f).Within(0.00001f));
            Assert.That(command.GeometryDigest, Is.EqualTo(456u));
        }

        [Test]
        public void TechniqueLifecycleTraversesAnticipationReleaseImpactAndSettle()
        {
            var timing = new EarthTechniqueTiming(0.2f, 0.1f, 0.15f, 0.3f);
            Assert.That(timing.Evaluate(-0.01f), Is.EqualTo(EarthTechniqueStage.Intent));
            Assert.That(timing.Evaluate(0.1f), Is.EqualTo(EarthTechniqueStage.Anticipation));
            Assert.That(timing.Evaluate(0.25f), Is.EqualTo(EarthTechniqueStage.Release));
            Assert.That(timing.Evaluate(0.38f), Is.EqualTo(EarthTechniqueStage.Impact));
            Assert.That(timing.Evaluate(0.6f), Is.EqualTo(EarthTechniqueStage.Settle));
            Assert.That(timing.Evaluate(0.76f), Is.EqualTo(EarthTechniqueStage.Complete));
        }

        private static void AssertTechnique(EarthTechniqueKind expected, EarthTechniqueContext context)
        {
            EarthTechniqueResolution resolution = EarthTechniqueRouter.Resolve(in context);
            Assert.That(resolution.Accepted, Is.True, resolution.Rejection.ToString());
            Assert.That(resolution.Technique, Is.EqualTo(expected));
        }

        private static EarthTechniqueContext Context(
            EarthTechniqueGesture gesture,
            EarthTechniqueModifierFlags modifiers,
            bool terrain = false,
            bool physicalTarget = false,
            bool broken = false,
            bool grounded = true,
            bool overMass = false,
            bool obstructed = false,
            bool provenance = true) =>
            new EarthTechniqueContext(
                gesture, modifiers, terrain, physicalTarget, broken,
                grounded, overMass, obstructed, provenance);
    }
}
