using System;
using System.Collections.Generic;
using System.IO;

namespace Elemental.Presentation.Rendering
{
    public enum Gate1CaptureVariant : byte
    {
        AnimationLegacy = 0,
        AnimationInertialization = 1,
        DuelNoShadows = 2,
        DuelShadowMap = 3,
        RecoveryLegacy = 4,
        RecoveryPoseMatched = 5
    }

    [Serializable]
    public sealed class Gate1CaptureFeatureState
    {
        public bool usePlayablesAnimationGraph;
        public bool usePoseInertialization;
        public bool useDuelShadowMap;
        public bool useDuelShadowDebugReceiver;
        public bool usePoseMatchedRecovery;
    }

    [Serializable]
    public sealed class Gate1CaptureFrameEvidence
    {
        public Gate1CaptureVariant variant;
        public string outputPath;
        public int unityFrame;
        public int imageByteCount;
        public float imageMinimumLuminance;
        public float imageMaximumLuminance;
        public float imageLuminanceRange;
        public Gate1CaptureFeatureState featureState = new Gate1CaptureFeatureState();
        public int featurePathEvidenceCount;
        public int animationGraphActiveFrames;
        public int animationTopologyValidFrames;
        public int animationInertiaActiveFrames;
        public int animationTransitionRequests;
        public int animationLegacyFrames;
        public int animationCurveOwnedParameterCount;
        public string animationCurveOwnedParameters;
        public int duelDisabledFrames;
        public int duelMapRenderedFrames;
        public int duelDrawnCasterCount;
        public int recoveryLegacyFrames;
        public int recoveryPoseMatchedFrames;
        public int recoveryStateVerifiedFrames;
        public int recoveryClearanceSucceededFrames;
        public int recoveryIsolatedSamplerFrames;
        public int recoveryLiveSupportFrames;
        public int recoveryPelvisContinuityVerifiedFrames;
        public float recoveryPelvisContinuityErrorMeters;
    }

    [Serializable]
    public sealed class Gate1RecoveryActorEvidence
    {
        public string actorName;
        public string hierarchyPath;
        public bool animatorIsHuman;
        public bool hasRagdollRig;
        public bool hasMotorRootBody;
        public bool motorHasStableSupportAtSelection;
    }

    [Serializable]
    public sealed class Gate1CaptureRestorationEvidence
    {
        public bool runtimeOverrideRestored;
        public bool boundsProviderRestored;
        public bool casterRegistryRestored;
        public bool animationGraphOwnershipRestored;
        public bool physicalAnimationOwnershipRestored;
        public bool editorSceneWasCleanBeforePlay;
        public bool editorSceneCleanAfterPlay;
        public int registryCountBefore;
        public int registryCountAfter;
        public int transientComponentCountAfter;
    }

    [Serializable]
    public sealed class Gate1CaptureManifest
    {
        public const string CurrentSchema = "elemental-gate1-transient-ab-v1";

        public string schema = CurrentSchema;
        public string unityVersion;
        public string scenePath;
        public string outputDirectory;
        public string startedUtc;
        public string completedUtc;
        public bool complete;
        public bool success;
        public string failure;
        public Gate1RecoveryActorEvidence recoveryActor =
            new Gate1RecoveryActorEvidence();
        public Gate1CaptureFrameEvidence[] frames = Array.Empty<Gate1CaptureFrameEvidence>();
        public Gate1CaptureRestorationEvidence restoration =
            new Gate1CaptureRestorationEvidence();
    }

    public readonly struct Gate1CaptureRequest
    {
        public const string Argument = "-elementalGate1Capture";
        public const string DefaultOutputDirectory = "BuildReports/Gate1AB";
        public const string ManifestFileName = "manifest.json";

        public Gate1CaptureRequest(string outputDirectory)
        {
            OutputDirectory = outputDirectory;
        }

        public string OutputDirectory { get; }
        public string ManifestPath => Path.Combine(OutputDirectory, ManifestFileName);

        public static bool TryParse(
            IReadOnlyList<string> arguments,
            out Gate1CaptureRequest request)
        {
            request = default;
            if (arguments == null) return false;
            for (int index = 0; index < arguments.Count - 1; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        Argument,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                string output = arguments[index + 1];
                if (string.IsNullOrWhiteSpace(output)) return false;
                request = new Gate1CaptureRequest(Path.GetFullPath(output));
                return true;
            }
            return false;
        }

        public static string FileNameFor(Gate1CaptureVariant variant)
        {
            switch (variant)
            {
                case Gate1CaptureVariant.AnimationLegacy:
                    return "animation-legacy.png";
                case Gate1CaptureVariant.AnimationInertialization:
                    return "animation-inertialization.png";
                case Gate1CaptureVariant.DuelNoShadows:
                    return "duel-no-shadows.png";
                case Gate1CaptureVariant.DuelShadowMap:
                    return "duel-shadow-map.png";
                case Gate1CaptureVariant.RecoveryLegacy:
                    return "recovery-legacy.png";
                case Gate1CaptureVariant.RecoveryPoseMatched:
                    return "recovery-pose-matched.png";
                default:
                    throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
            }
        }
    }

    public static class Gate1CaptureEvidenceValidator
    {
        public const int RequiredCaptureCount = 6;

        public static bool TryValidate(
            Gate1CaptureManifest manifest,
            out string failure)
        {
            if (manifest == null)
                return Fail("The Gate1 manifest is null.", out failure);
            if (!string.Equals(
                    manifest.schema,
                    Gate1CaptureManifest.CurrentSchema,
                    StringComparison.Ordinal))
                return Fail("The Gate1 manifest schema is not supported.", out failure);
            if (manifest.frames == null ||
                manifest.frames.Length != RequiredCaptureCount)
                return Fail("Gate1 requires exactly six A/B capture frames.", out failure);
            if (manifest.recoveryActor == null ||
                string.IsNullOrWhiteSpace(manifest.recoveryActor.actorName) ||
                string.IsNullOrWhiteSpace(manifest.recoveryActor.hierarchyPath) ||
                !manifest.recoveryActor.animatorIsHuman ||
                !manifest.recoveryActor.hasRagdollRig ||
                !manifest.recoveryActor.hasMotorRootBody ||
                !manifest.recoveryActor.motorHasStableSupportAtSelection)
                return Fail("Gate1 recovery actor selection is absent or unsupported.", out failure);

            var seen = new bool[RequiredCaptureCount];
            for (int index = 0; index < manifest.frames.Length; index++)
            {
                Gate1CaptureFrameEvidence frame = manifest.frames[index];
                if (frame == null || frame.featureState == null)
                    return Fail("A Gate1 frame or feature-state record is null.", out failure);
                int variantIndex = (int)frame.variant;
                if (variantIndex < 0 || variantIndex >= RequiredCaptureCount ||
                    seen[variantIndex])
                    return Fail("Gate1 frame variants must be unique and complete.", out failure);
                seen[variantIndex] = true;
                if (frame.imageByteCount <= 0 || string.IsNullOrWhiteSpace(frame.outputPath))
                    return Fail($"{frame.variant} has no non-empty image evidence.", out failure);
                if (frame.featurePathEvidenceCount <= 0)
                    return Fail($"{frame.variant} has no executed feature-path evidence.", out failure);
                if (!ValidateVariant(frame, out failure)) return false;
            }

            for (int index = 0; index < seen.Length; index++)
                if (!seen[index])
                    return Fail("Gate1 is missing a required A/B variant.", out failure);
            failure = string.Empty;
            return true;
        }

        private static bool ValidateVariant(
            Gate1CaptureFrameEvidence frame,
            out string failure)
        {
            Gate1CaptureFeatureState state = frame.featureState;
            switch (frame.variant)
            {
                case Gate1CaptureVariant.AnimationLegacy:
                    if (!state.usePlayablesAnimationGraph &&
                        !state.usePoseInertialization &&
                        frame.animationLegacyFrames > 0)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
                case Gate1CaptureVariant.AnimationInertialization:
                    if (state.usePlayablesAnimationGraph &&
                        state.usePoseInertialization &&
                        frame.animationGraphActiveFrames > 0 &&
                        frame.animationTopologyValidFrames > 0 &&
                        frame.animationTransitionRequests > 0)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
                case Gate1CaptureVariant.DuelNoShadows:
                    if (!state.useDuelShadowMap && frame.duelDisabledFrames > 0)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
                case Gate1CaptureVariant.DuelShadowMap:
                    if (state.useDuelShadowMap &&
                        state.useDuelShadowDebugReceiver &&
                        frame.duelMapRenderedFrames > 0 &&
                        frame.duelDrawnCasterCount > 0 &&
                        frame.imageLuminanceRange > 0.01f)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
                case Gate1CaptureVariant.RecoveryLegacy:
                    if (!state.usePoseMatchedRecovery && frame.recoveryLegacyFrames > 0)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
                case Gate1CaptureVariant.RecoveryPoseMatched:
                    if (state.usePoseMatchedRecovery &&
                        frame.recoveryPoseMatchedFrames > 0 &&
                        frame.recoveryStateVerifiedFrames > 0 &&
                        frame.recoveryClearanceSucceededFrames > 0 &&
                        frame.recoveryIsolatedSamplerFrames > 0 &&
                        frame.recoveryLiveSupportFrames > 0 &&
                        frame.recoveryPelvisContinuityVerifiedFrames > 0 &&
                        frame.recoveryPelvisContinuityErrorMeters >= 0f &&
                        frame.recoveryPelvisContinuityErrorMeters <=
                            Gate1RecoverySampleMath.MaximumPelvisContinuityErrorMeters)
                    {
                        failure = string.Empty;
                        return true;
                    }
                    break;
            }

            return Fail($"{frame.variant} contradicts its explicit feature state or metrics.", out failure);
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }
    }
}
