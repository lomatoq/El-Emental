using Elemental.Presentation.Camera;
using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthGameplayCameraFramingTests
    {
        [Test]
        public void ExploreHeightIncludesPitchInducedDistanceLift()
        {
            const float desiredHeight = 2.7f;
            float arm = EarthCameraRigFramingSolver.ResolveVerticalArmLength(
                desiredHeight, .92f, .32f, 7.65f, 11f, 0f, 1.15f);
            float actual = EarthCameraRigFramingSolver.ResolveCameraHeight(
                .92f, .32f, arm, 7.65f, 11f);

            Assert.That(arm, Is.InRange(0f, .03f));
            Assert.That(actual, Is.EqualTo(desiredHeight).Within(.02f));
        }

        [Test]
        public void OldArmFloorWouldRaiseExploreCameraByMoreThanFortyCentimeters()
        {
            float corrected = EarthCameraRigFramingSolver.ResolveVerticalArmLength(
                2.7f, .92f, .32f, 7.65f, 11f, 0f, 1.15f);
            float correctedHeight = EarthCameraRigFramingSolver.ResolveCameraHeight(
                .92f, .32f, corrected, 7.65f, 11f);
            float oldFloorHeight = EarthCameraRigFramingSolver.ResolveCameraHeight(
                .92f, .32f, .42f, 7.65f, 11f);

            Assert.That(oldFloorHeight - correctedHeight, Is.GreaterThan(.40f));
        }

        [TestCase(EarthCameraState.Explore, 7.65f, 2.7f, 11f)]
        [TestCase(EarthCameraState.Aim, 7.5f, 2.6f, 11f)]
        [TestCase(EarthCameraState.DrawStructure, 8.2f, 3.2f, 14f)]
        [TestCase(EarthCameraState.HoldMass, 7.9f, 2.95f, 12.5f)]
        [TestCase(EarthCameraState.Airborne, 8.05f, 3.25f, 13f)]
        public void StateProfilesRemainFiniteAndCloseToTheirAuthoredHeight(
            EarthCameraState _, float distance, float height, float pitch)
        {
            float arm = EarthCameraRigFramingSolver.ResolveVerticalArmLength(
                height, .92f, .32f, distance, pitch, 0f, 1.15f);
            float actual = EarthCameraRigFramingSolver.ResolveCameraHeight(
                .92f, .32f, arm, distance, pitch);

            Assert.That(float.IsFinite(arm), Is.True);
            Assert.That(arm, Is.InRange(0f, 1.15f));
            Assert.That(actual, Is.InRange(height - .02f, height + .16f));
        }
    }
}
