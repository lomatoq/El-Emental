using Elemental.Presentation.Rendering;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class VisualQaCaptureRequestTests
    {
        [Test]
        public void TryParse_RequiresExplicitArgumentAndOutputPath()
        {
            Assert.That(VisualQaCaptureRequest.TryParse(new[] { "game.exe" }, out _), Is.False);
            Assert.That(VisualQaCaptureRequest.TryParse(
                new[] { "game.exe", VisualQaCaptureRequest.Argument, "capture.png" }, out VisualQaCaptureRequest request), Is.True);
            Assert.That(request.OutputPath, Is.EqualTo("capture.png"));
            Assert.That(request.DemonstrateMagic, Is.False);
            Assert.That(request.Scenario, Is.EqualTo(VisualQaScenario.None));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe",
                    VisualQaCaptureRequest.MagicArgument,
                    VisualQaCaptureRequest.Argument,
                    "magic.png"
                },
                out VisualQaCaptureRequest magicRequest), Is.True);
            Assert.That(magicRequest.DemonstrateMagic, Is.True);
            Assert.That(magicRequest.Scenario, Is.EqualTo(VisualQaScenario.Wall));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe", VisualQaCaptureRequest.Argument, "pull.png",
                    VisualQaCaptureRequest.ScenarioArgument, "pull-held"
                }, out VisualQaCaptureRequest pullRequest), Is.True);
            Assert.That(pullRequest.Scenario, Is.EqualTo(VisualQaScenario.PullHeld));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe", VisualQaCaptureRequest.Argument, "collapse.png",
                    VisualQaCaptureRequest.ScenarioArgument, "wall-collapse"
                }, out VisualQaCaptureRequest collapseRequest), Is.True);
            Assert.That(collapseRequest.Scenario, Is.EqualTo(VisualQaScenario.WallCollapse));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe", VisualQaCaptureRequest.Argument, "platform.png",
                    VisualQaCaptureRequest.ScenarioArgument, "platform"
                }, out VisualQaCaptureRequest platformRequest), Is.True);
            Assert.That(platformRequest.Scenario, Is.EqualTo(VisualQaScenario.Platform));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe", VisualQaCaptureRequest.Argument, "gravity.png",
                    VisualQaCaptureRequest.ScenarioArgument, "gravity"
                }, out VisualQaCaptureRequest gravityRequest), Is.True);
            Assert.That(gravityRequest.Scenario, Is.EqualTo(VisualQaScenario.GravityWell));

            Assert.That(VisualQaCaptureRequest.TryParse(
                new[]
                {
                    "game.exe", VisualQaCaptureRequest.Argument, "night.png",
                    VisualQaCaptureRequest.ScenarioArgument, "night"
                }, out VisualQaCaptureRequest nightRequest), Is.True);
            Assert.That(nightRequest.Scenario, Is.EqualTo(VisualQaScenario.Night));
        }
    }
}
