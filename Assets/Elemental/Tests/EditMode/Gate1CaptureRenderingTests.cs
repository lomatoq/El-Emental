using System;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class Gate1CaptureRenderingTests
    {
        [Test]
        public void RequestParserProducesDeterministicOutputContract()
        {
            string[] arguments =
            {
                "Unity.exe",
                Gate1CaptureRequest.Argument,
                "BuildReports/Gate1-Test"
            };

            Assert.That(Gate1CaptureRequest.TryParse(
                arguments,
                out Gate1CaptureRequest request), Is.True);
            Assert.That(request.OutputDirectory, Does.EndWith("Gate1-Test"));
            Assert.That(request.ManifestPath, Does.EndWith("manifest.json"));
            Assert.That(Gate1CaptureRequest.FileNameFor(
                Gate1CaptureVariant.AnimationLegacy), Is.EqualTo("animation-legacy.png"));
            Assert.That(Gate1CaptureRequest.FileNameFor(
                Gate1CaptureVariant.DuelShadowMap), Is.EqualTo("duel-shadow-map.png"));
            Assert.That(Gate1CaptureRequest.FileNameFor(
                Gate1CaptureVariant.RecoveryPoseMatched),
                Is.EqualTo("recovery-pose-matched.png"));
        }

        [Test]
        public void EvidenceValidatorAcceptsOnlyCompleteNonemptyFeaturePaths()
        {
            Gate1CaptureManifest manifest = CompleteManifest();

            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out string failure), Is.True, failure);

            manifest.frames[(int)Gate1CaptureVariant.DuelShadowMap]
                .featurePathEvidenceCount = 0;
            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out failure), Is.False);
            StringAssert.Contains("no executed feature-path evidence", failure);
        }

        [Test]
        public void EvidenceValidatorRejectsFeatureStateMetricContradiction()
        {
            Gate1CaptureManifest manifest = CompleteManifest();
            manifest.frames[(int)Gate1CaptureVariant.RecoveryPoseMatched]
                .featureState.usePoseMatchedRecovery = false;

            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out string failure), Is.False);
            StringAssert.Contains("contradicts", failure);
        }

        [Test]
        public void EvidenceValidatorRejectsPoseMatchedPelvisContinuityBreach()
        {
            Gate1CaptureManifest manifest = CompleteManifest();
            manifest.frames[(int)Gate1CaptureVariant.RecoveryPoseMatched]
                .recoveryPelvisContinuityErrorMeters =
                Gate1RecoverySampleMath.MaximumPelvisContinuityErrorMeters + 0.001f;

            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out string failure), Is.False);
            StringAssert.Contains("contradicts", failure);
        }

        [Test]
        public void EvidenceValidatorRejectsRecoveryWithoutLiveSupport()
        {
            Gate1CaptureManifest manifest = CompleteManifest();
            manifest.frames[(int)Gate1CaptureVariant.RecoveryPoseMatched]
                .recoveryLiveSupportFrames = 0;

            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out string failure), Is.False);
            StringAssert.Contains("contradicts", failure);
        }

        [Test]
        public void EvidenceValidatorRejectsUnsupportedRecoveryActorSelection()
        {
            Gate1CaptureManifest manifest = CompleteManifest();
            manifest.recoveryActor.motorHasStableSupportAtSelection = false;

            Assert.That(Gate1CaptureEvidenceValidator.TryValidate(
                manifest,
                out string failure), Is.False);
            StringAssert.Contains("actor selection", failure);
        }

        [Test]
        public void DuelCaptureOverrideIsSingleOwnerAndRestoresDefaultOff()
        {
            DuelShadowRuntimeSettings settings = CaptureSettings();
            Assert.That(DuelShadowCaptureOverride.IsActive, Is.False);
            DuelShadowCaptureOverride.Token token = default;
            try
            {
                Assert.That(DuelShadowCaptureOverride.TryBegin(
                    in settings,
                    out token,
                    out string failure), Is.True, failure);
                Assert.That(DuelShadowCaptureOverride.IsActive, Is.True);
                Assert.That(DuelShadowCaptureOverride.TryBegin(
                    in settings,
                    out _,
                    out failure), Is.False);
                StringAssert.Contains("already owns", failure);

                token.Dispose();
                token.Dispose();
                Assert.That(DuelShadowCaptureOverride.IsActive, Is.False);
            }
            finally
            {
                token.Dispose();
            }
        }

        [Test]
        public void RecoverySamplePelvisOffsetUsesMotorRootConvention()
        {
            Vector3 rootPosition = new Vector3(4f, 5f, 6f);
            Quaternion rootRotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 expectedLocalOffset = new Vector3(0.2f, 0.93f, -0.15f);
            Vector3 pelvisWorld = rootPosition + rootRotation * expectedLocalOffset;

            Vector3 resolved = Gate1RecoverySampleMath.MotorRootPelvisOffset(
                pelvisWorld,
                rootPosition,
                rootRotation);

            Assert.That(resolved.x, Is.EqualTo(expectedLocalOffset.x).Within(0.00001f));
            Assert.That(resolved.y, Is.EqualTo(expectedLocalOffset.y).Within(0.00001f));
            Assert.That(resolved.z, Is.EqualTo(expectedLocalOffset.z).Within(0.00001f));
        }

        [Test]
        public void RecoveryContinuityRemovesAuthoredClearanceLift()
        {
            Vector3 livePelvis = new Vector3(3.2f, 1.1f, -4.7f);
            Quaternion rootRotation = Quaternion.Euler(0f, 35f, 0f);
            Vector3 localOffset = new Vector3(0.04f, 0.91f, -0.08f);
            Vector3 up = Vector3.up;
            const float clearanceLift = 0.12f;
            Vector3 alignedRoot = livePelvis - rootRotation * localOffset +
                                  up * clearanceLift;

            Vector3 reconstructed = Gate1RecoverySampleMath.ReconstructPreClearancePelvis(
                alignedRoot,
                rootRotation,
                localOffset,
                up,
                clearanceLift);

            Assert.That(Vector3.Distance(reconstructed, livePelvis), Is.LessThan(0.00001f));
        }

        private static Gate1CaptureManifest CompleteManifest()
        {
            var frames = new Gate1CaptureFrameEvidence[6];
            frames[0] = Frame(Gate1CaptureVariant.AnimationLegacy);
            frames[0].animationLegacyFrames = 1;
            frames[1] = Frame(Gate1CaptureVariant.AnimationInertialization);
            frames[1].featureState.usePlayablesAnimationGraph = true;
            frames[1].featureState.usePoseInertialization = true;
            frames[1].animationGraphActiveFrames = 1;
            frames[1].animationTopologyValidFrames = 1;
            frames[1].animationTransitionRequests = 1;
            frames[1].animationCurveOwnedParameterCount = 1;
            frames[1].animationCurveOwnedParameters = "MotionTime(123456)";
            frames[2] = Frame(Gate1CaptureVariant.DuelNoShadows);
            frames[2].duelDisabledFrames = 1;
            frames[3] = Frame(Gate1CaptureVariant.DuelShadowMap);
            frames[3].featureState.useDuelShadowMap = true;
            frames[3].featureState.useDuelShadowDebugReceiver = true;
            frames[3].duelMapRenderedFrames = 1;
            frames[3].duelDrawnCasterCount = 2;
            frames[4] = Frame(Gate1CaptureVariant.RecoveryLegacy);
            frames[4].recoveryLegacyFrames = 1;
            frames[5] = Frame(Gate1CaptureVariant.RecoveryPoseMatched);
            frames[5].featureState.usePoseMatchedRecovery = true;
            frames[5].recoveryPoseMatchedFrames = 1;
            frames[5].recoveryStateVerifiedFrames = 1;
            frames[5].recoveryClearanceSucceededFrames = 1;
            frames[5].recoveryIsolatedSamplerFrames = 1;
            frames[5].recoveryLiveSupportFrames = 1;
            frames[5].recoveryPelvisContinuityVerifiedFrames = 1;
            frames[5].recoveryPelvisContinuityErrorMeters = 0.0001f;
            return new Gate1CaptureManifest
            {
                recoveryActor = new Gate1RecoveryActorEvidence
                {
                    actorName = "Player Presentation",
                    hierarchyPath = "Planet Character/Player Presentation",
                    animatorIsHuman = true,
                    hasRagdollRig = true,
                    hasMotorRootBody = true,
                    motorHasStableSupportAtSelection = true
                },
                frames = frames
            };
        }

        private static Gate1CaptureFrameEvidence Frame(Gate1CaptureVariant variant)
        {
            return new Gate1CaptureFrameEvidence
            {
                variant = variant,
                outputPath = Gate1CaptureRequest.FileNameFor(variant),
                imageByteCount = 1,
                imageLuminanceRange = 0.25f,
                featurePathEvidenceCount = 1,
                featureState = new Gate1CaptureFeatureState()
            };
        }

        private static DuelShadowRuntimeSettings CaptureSettings()
        {
            return new DuelShadowRuntimeSettings(
                DuelShadowQuality.Resolve(DuelShadowQualityTier.Low),
                new DuelShadowClassificationSettings(0.45f, 0.8f),
                new DuelShadowStabilizationSettings(
                    12f, 160f, 1.5f, 4f, 0.5f, 1f, 0.2f, 1f, 1.5f),
                16,
                0.88f,
                0.8f,
                1.8f,
                DuelShadowDebugView.ShadowOnly);
        }
    }
}
