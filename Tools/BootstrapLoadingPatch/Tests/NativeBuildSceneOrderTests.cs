using System;
using Elemental.Authoring.Build;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class NativeBuildSceneOrderTests
    {
        [Test]
        public void Create_PutsBootstrapThenPlayableFirstAndPreservesRemainingOrder()
        {
            string[] result = NativeBuildSceneOrder.Create(new[]
            {
                "Bootstrap.unity", "Main.unity", "EarthCoreSlice.unity", "GravityToy.unity"
            }, "Bootstrap.unity", "EarthCoreSlice.unity");

            Assert.That(result, Is.EqualTo(new[]
            {
                "Bootstrap.unity", "EarthCoreSlice.unity", "Main.unity", "GravityToy.unity"
            }));
        }

        [TestCase("MissingBootstrap.unity", "EarthCoreSlice.unity", "bootstrap")]
        [TestCase("Bootstrap.unity", "MissingPlayable.unity", "playable")]
        public void Create_RejectsMissingRequiredScene(string bootstrap, string playable, string role)
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                NativeBuildSceneOrder.Create(new[] { "Bootstrap.unity", "EarthCoreSlice.unity" }, bootstrap, playable));
            Assert.That(exception.Message, Does.Contain(role));
            Assert.That(exception.Message, Does.Contain("not enabled"));
        }

        [Test]
        public void Create_ExcludesEditorOnlyPolishLab()
        {
            string[] result = NativeBuildSceneOrder.Create(new[]
            {
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",
                "Assets/Elemental/Content/Scenes/EarthPolishLab.unity",
                "Assets/Elemental/Content/Scenes/Bootstrap.unity"
            }, "Assets/Elemental/Content/Scenes/Bootstrap.unity",
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");

            Assert.That(result, Is.EqualTo(new[]
            {
                "Assets/Elemental/Content/Scenes/Bootstrap.unity",
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity"
            }));
        }
    }
}
