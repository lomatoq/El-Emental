using Elemental.Authoring.Editor;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationAssetValidatorTests
    {
        [Test]
        public void PointerPayload_IsRejectedBeforeUnityImport()
        {
            Assert.That(EarthAnimationAssetValidator.IsGitLfsPointerText(
                "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 42"), Is.True);
            Assert.That(EarthAnimationAssetValidator.IsGitLfsPointerText("KayKit binary payload"), Is.False);
        }

        [Test]
        public void ClipDurations_RejectEmptyZeroAndNonFiniteData()
        {
            Assert.That(EarthAnimationAssetValidator.HasUsableClipDurations(System.Array.Empty<float>()), Is.False);
            Assert.That(EarthAnimationAssetValidator.HasUsableClipDurations(new[] { 0f }), Is.False);
            Assert.That(EarthAnimationAssetValidator.HasUsableClipDurations(new[] { float.NaN }), Is.False);
            Assert.That(EarthAnimationAssetValidator.HasUsableClipDurations(new[] { 0.32f, 1.1f }), Is.True);
        }

        [Test]
        public void AuditedKayKitAssets_PassProjectGate()
        {
            EarthAnimationValidationReport report = EarthAnimationAssetValidator.ValidateProject();
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
        }
    }
}
