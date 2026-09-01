using Elemental.Authoring.Editor;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthAnimationContentAuditTests
    {
        private EarthMotionCatalog _catalog;

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void AvailabilityFailsClosedAtFirstMissingEvidence()
        {
            var missingSource = new EarthAnimationContentAvailability(
                EarthAnimationContentFamily.AuthoredFlip,
                EarthAnimationContentQuality.Missing,
                false,
                false,
                false);
            var missingCatalog = new EarthAnimationContentAvailability(
                EarthAnimationContentFamily.MagicLift,
                EarthAnimationContentQuality.CompatibleAuthored,
                true,
                false,
                true);
            var missingBinding = new EarthAnimationContentAvailability(
                EarthAnimationContentFamily.MagicLift,
                EarthAnimationContentQuality.CompatibleAuthored,
                true,
                true,
                false);
            var fallback = new EarthAnimationContentAvailability(
                EarthAnimationContentFamily.MagicPull,
                EarthAnimationContentQuality.GenericFallback,
                true,
                true,
                true);

            Assert.That(missingSource.Blocker,
                Is.EqualTo(EarthAnimationContentBlocker.MissingSourceClip));
            Assert.That(missingCatalog.Blocker,
                Is.EqualTo(EarthAnimationContentBlocker.MissingCatalogProfile));
            Assert.That(missingBinding.Blocker,
                Is.EqualTo(EarthAnimationContentBlocker.MissingControllerBinding));
            Assert.That(fallback.IsRuntimePlayable, Is.True);
            Assert.That(fallback.IsAuthoredCoverage, Is.False);
            Assert.That(fallback.Blocker,
                Is.EqualTo(EarthAnimationContentBlocker.GenericFallbackOnly));
        }

        [Test]
        public void CanonicalCatalogAndControllerExposeOnlyTruthfulAuthoredFamilies()
        {
            _catalog = ScriptableObject.CreateInstance<EarthMotionCatalog>();
            EarthMotionCatalogBuilder.Rebuild(_catalog);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    EarthHumanoidMotionSetup.ControllerPath);
            Assert.That(controller, Is.Not.Null);

            EarthAnimationContentFamily[] authored =
            {
                EarthAnimationContentFamily.PivotLeft,
                EarthAnimationContentFamily.PivotRight,
                EarthAnimationContentFamily.MagicGather,
                EarthAnimationContentFamily.MagicLift,
                EarthAnimationContentFamily.MagicSustain,
                EarthAnimationContentFamily.MagicRelease
            };
            for (int index = 0; index < authored.Length; index++)
            {
                EarthAnimationContentAuditEntry entry =
                    EarthAnimationContentAudit.Evaluate(
                        _catalog,
                        controller,
                        authored[index]);
                Assert.That(entry.Availability.IsAuthoredCoverage,
                    Is.True,
                    $"{entry.Family}: {entry.Availability.Blocker} " +
                    $"{entry.SourceAssetPath}#{entry.SourceClipName}");
                Assert.That(entry.Availability.Blocker,
                    Is.EqualTo(EarthAnimationContentBlocker.None));
            }

            EarthAnimationContentFamily[] fallbackOnly =
            {
                EarthAnimationContentFamily.DirectionalStart,
                EarthAnimationContentFamily.DirectionalStop,
                EarthAnimationContentFamily.MagicPull,
                EarthAnimationContentFamily.MagicPush,
                EarthAnimationContentFamily.MagicSlam,
                EarthAnimationContentFamily.RecoveryFront,
                EarthAnimationContentFamily.RecoveryBack
            };
            for (int index = 0; index < fallbackOnly.Length; index++)
            {
                EarthAnimationContentAvailability availability =
                    EarthAnimationContentAudit.Evaluate(
                        _catalog,
                        controller,
                        fallbackOnly[index]).Availability;
                Assert.That(availability.IsRuntimePlayable, Is.True, fallbackOnly[index].ToString());
                Assert.That(availability.IsAuthoredCoverage, Is.False, fallbackOnly[index].ToString());
                Assert.That(availability.Blocker,
                    Is.EqualTo(EarthAnimationContentBlocker.GenericFallbackOnly));
            }

            EarthAnimationContentAuditEntry flip = EarthAnimationContentAudit.Evaluate(
                _catalog,
                controller,
                EarthAnimationContentFamily.AuthoredFlip);
            Assert.That(flip.SourceAssetPath, Is.Empty);
            Assert.That(flip.Availability.IsRuntimePlayable, Is.False);
            Assert.That(flip.Availability.Blocker,
                Is.EqualTo(EarthAnimationContentBlocker.MissingSourceClip));
        }
    }
}
