using System;
using Elemental.Authoring.Build;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class NativeBuildSceneOrderTests
    {
        [Test]
        public void Create_PutsPlayableSceneFirstAndPreservesRemainingOrder()
        {
            string[] scenes =
            {
                "Bootstrap.unity",
                "Main.unity",
                "EarthCoreSlice.unity",
                "GravityToy.unity"
            };

            string[] result = NativeBuildSceneOrder.Create(scenes, "EarthCoreSlice.unity");

            Assert.That(result, Is.EqualTo(new[]
            {
                "EarthCoreSlice.unity",
                "Bootstrap.unity",
                "Main.unity",
                "GravityToy.unity"
            }));
        }

        [Test]
        public void Create_RejectsMissingPlayableSceneWithActionableMessage()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                NativeBuildSceneOrder.Create(new[] { "Bootstrap.unity" }, "EarthCoreSlice.unity"));

            Assert.That(exception.Message, Does.Contain("EarthCoreSlice.unity"));
            Assert.That(exception.Message, Does.Contain("not enabled"));
        }

        [Test]
        public void Create_ExcludesEditorOnlyPolishLabEvenWhenEnabledForPlayModeTests()
        {
            string[] result = NativeBuildSceneOrder.Create(new[]
            {
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",
                "Assets/Elemental/Content/Scenes/EarthPolishLab.unity",
                "Assets/Elemental/Content/Scenes/Bootstrap.unity"
            }, "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");

            Assert.That(result, Is.EqualTo(new[]
            {
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",
                "Assets/Elemental/Content/Scenes/Bootstrap.unity"
            }));
        }
    }
}
