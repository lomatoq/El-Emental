using System;
using NUnit.Framework;

namespace Elemental.Online.Tests
{
    public sealed class OnlineLaunchProfileTests
    {
        [Test]
        public void TwoProcessesCanSelectIndependentProfiles()
        {
            Assert.That(OnlineLaunchProfile.Parse(new[] { "game.exe", "--online-profile", "peer-one" }), Is.EqualTo("peer-one"));
            Assert.That(OnlineLaunchProfile.Parse(new[] { "game.exe", "--online-profile", "peer-two" }), Is.EqualTo("peer-two"));
            Assert.That(OnlineLaunchProfile.Parse(new[] { "game.exe" }), Is.Null);
        }
        [TestCase("bad/profile")]
        [TestCase("a profile")]
        [TestCase("")]
        public void InvalidProfileCannotAliasAnotherProcessStorage(string profile)
            => Assert.Throws<ArgumentException>(() => OnlineLaunchProfile.Parse(new[] { "--online-profile", profile }));
        [Test]
        public void MissingOrDuplicateProfileFailsExplicitly()
        {
            Assert.Throws<ArgumentException>(() => OnlineLaunchProfile.Parse(new[] { "--online-profile" }));
            Assert.Throws<ArgumentException>(() => OnlineLaunchProfile.Parse(new[] { "--online-profile", "one", "--online-profile", "two" }));
        }
    }
}
