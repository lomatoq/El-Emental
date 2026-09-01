using System;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public readonly struct EarthAnimationContentAuditEntry
    {
        public EarthAnimationContentAuditEntry(
            EarthAnimationContentFamily family,
            string sourceAssetPath,
            string sourceClipName,
            string controllerStatePath,
            in EarthAnimationContentAvailability availability)
        {
            Family = family;
            SourceAssetPath = sourceAssetPath ?? string.Empty;
            SourceClipName = sourceClipName ?? string.Empty;
            ControllerStatePath = controllerStatePath ?? string.Empty;
            Availability = availability;
        }

        public EarthAnimationContentFamily Family { get; }
        public string SourceAssetPath { get; }
        public string SourceClipName { get; }
        public string ControllerStatePath { get; }
        public EarthAnimationContentAvailability Availability { get; }
    }

    /// <summary>
    /// Read-only content audit. It reports the imported clip identity verbatim and
    /// never upgrades a generic fallback into an authored-content claim.
    /// </summary>
    public static class EarthAnimationContentAudit
    {
        private const string LocomotionState = "Base Layer.Locomotion";
        private const string TurnState = "Base Layer.Turn In Place";
        private const string CastState = "Earth Magic Upper Body.Earth Cast";
        private const string RecoveryState = "Base Layer.Knockdown Recovery";

        private readonly struct Descriptor
        {
            public Descriptor(
                EarthAnimationContentFamily family,
                EarthAnimationContentQuality quality,
                string path,
                string clipName,
                string statePath,
                string directParameter = null,
                bool requireMirror = false)
            {
                Family = family;
                Quality = quality;
                Path = path;
                ClipName = clipName;
                StatePath = statePath;
                DirectParameter = directParameter;
                RequireMirror = requireMirror;
            }

            public EarthAnimationContentFamily Family { get; }
            public EarthAnimationContentQuality Quality { get; }
            public string Path { get; }
            public string ClipName { get; }
            public string StatePath { get; }
            public string DirectParameter { get; }
            public bool RequireMirror { get; }
        }

        private static readonly Descriptor[] Descriptors =
        {
            new(EarthAnimationContentFamily.DirectionalStart,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.KayKitMovementBasicPath,
                "Running_A", LocomotionState),
            new(EarthAnimationContentFamily.DirectionalStop,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.KayKitMovementBasicPath,
                "Running_A", LocomotionState),
            new(EarthAnimationContentFamily.PivotLeft,
                EarthAnimationContentQuality.ExactAuthored,
                EarthHumanoidMotionSetup.LeftTurnPath,
                "Left Turn", TurnState, requireMirror: false),
            new(EarthAnimationContentFamily.PivotRight,
                EarthAnimationContentQuality.MirroredAuthored,
                EarthHumanoidMotionSetup.LeftTurnPath,
                "Left Turn", TurnState, requireMirror: true),
            new(EarthAnimationContentFamily.MagicGather,
                EarthAnimationContentQuality.CompatibleAuthored,
                EarthHumanoidMotionSetup.KayKitCombatRangedPath,
                EarthHumanoidMotionSetup.MagicSummonClipName,
                CastState, "EarthPose09"),
            new(EarthAnimationContentFamily.MagicPull,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.Magic2HCast01Path,
                "Standing 2H Cast Spell 01", CastState, "EarthPose03"),
            new(EarthAnimationContentFamily.MagicPush,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.KayKitCombatRangedPath,
                EarthHumanoidMotionSetup.MagicShootClipName,
                CastState, "EarthPose05"),
            new(EarthAnimationContentFamily.MagicLift,
                EarthAnimationContentQuality.CompatibleAuthored,
                EarthHumanoidMotionSetup.KayKitCombatRangedPath,
                EarthHumanoidMotionSetup.MagicRaiseClipName,
                CastState, "EarthPose01"),
            new(EarthAnimationContentFamily.MagicSlam,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.Magic2HAttack03Path,
                "Standing 2H Magic Attack 03", CastState, "EarthPose07"),
            new(EarthAnimationContentFamily.MagicSustain,
                EarthAnimationContentQuality.CompatibleAuthored,
                EarthHumanoidMotionSetup.KayKitCombatRangedPath,
                EarthHumanoidMotionSetup.MagicSpellcastingLongClipName,
                CastState, "EarthPose06"),
            new(EarthAnimationContentFamily.MagicRelease,
                EarthAnimationContentQuality.CompatibleAuthored,
                EarthHumanoidMotionSetup.KayKitCombatRangedPath,
                EarthHumanoidMotionSetup.MagicShootClipName,
                CastState, "EarthPose04"),
            new(EarthAnimationContentFamily.RecoveryFront,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.FallingRollPath,
                "Falling To Roll", RecoveryState),
            new(EarthAnimationContentFamily.RecoveryBack,
                EarthAnimationContentQuality.GenericFallback,
                EarthHumanoidMotionSetup.FallingRollPath,
                "Falling To Roll", RecoveryState),
            new(EarthAnimationContentFamily.AuthoredFlip,
                EarthAnimationContentQuality.Missing,
                null, null, null)
        };

        public static int FamilyCount => Descriptors.Length;

        public static EarthAnimationContentAuditEntry Evaluate(
            EarthMotionCatalog catalog,
            AnimatorController controller,
            EarthAnimationContentFamily family)
        {
            Descriptor descriptor = FindDescriptor(family);
            AnimationClip clip = LoadExactClip(descriptor.Path, descriptor.ClipName);
            bool source = clip != null;
            bool cataloged = source && catalog != null &&
                             AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                 clip,
                                 out string guid,
                                 out long localFileId) &&
                             catalog.TryFind(guid, localFileId, out _);
            bool bound = source && controller != null &&
                         HasControllerBinding(controller, descriptor, clip);
            var availability = new EarthAnimationContentAvailability(
                family,
                descriptor.Quality,
                source,
                cataloged,
                bound);
            return new EarthAnimationContentAuditEntry(
                family,
                descriptor.Path,
                descriptor.ClipName,
                descriptor.StatePath,
                in availability);
        }

        [MenuItem("Elemental Suite/Character/Audit Authored Animation Content")]
        public static void AuditDefaultContent()
        {
            EarthMotionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<EarthMotionCatalog>(
                    EarthMotionCatalogBuilder.DefaultCatalogPath);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    EarthHumanoidMotionSetup.ControllerPath);
            for (int index = 0; index < Descriptors.Length; index++)
            {
                EarthAnimationContentAuditEntry entry = Evaluate(
                    catalog,
                    controller,
                    Descriptors[index].Family);
                string source = string.IsNullOrEmpty(entry.SourceAssetPath)
                    ? "<no imported source>"
                    : $"{entry.SourceAssetPath}#{entry.SourceClipName}";
                Debug.Log(
                    $"[Elemental][AnimationContent] {entry.Family}: " +
                    $"quality={entry.Availability.Quality}, " +
                    $"authored={entry.Availability.IsAuthoredCoverage}, " +
                    $"runtime={entry.Availability.IsRuntimePlayable}, " +
                    $"blocker={entry.Availability.Blocker}, source={source}, " +
                    $"state={entry.ControllerStatePath}.");
            }
        }

        private static Descriptor FindDescriptor(EarthAnimationContentFamily family)
        {
            for (int index = 0; index < Descriptors.Length; index++)
                if (Descriptors[index].Family == family) return Descriptors[index];
            throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }

        private static AnimationClip LoadExactClip(string path, string clipName)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(clipName)) return null;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            AnimationClip match = null;
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is not AnimationClip clip ||
                    !string.Equals(clip.name, clipName, StringComparison.Ordinal))
                    continue;
                if (match != null)
                    throw new InvalidOperationException(
                        $"Animation source '{path}' contains duplicate clips named '{clipName}'.");
                match = clip;
            }
            return match;
        }

        private static bool HasControllerBinding(
            AnimatorController controller,
            in Descriptor descriptor,
            AnimationClip clip)
        {
            if (!TryFindState(controller, descriptor.StatePath, out AnimatorState state))
                return false;
            return MotionContains(
                state.motion,
                clip,
                descriptor.DirectParameter,
                descriptor.RequireMirror);
        }

        private static bool TryFindState(
            AnimatorController controller,
            string statePath,
            out AnimatorState state)
        {
            state = null;
            if (string.IsNullOrEmpty(statePath)) return false;
            int separator = statePath.IndexOf('.');
            if (separator <= 0 || separator >= statePath.Length - 1) return false;
            string layerName = statePath.Substring(0, separator);
            string stateName = statePath.Substring(separator + 1);
            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                if (!string.Equals(layers[layerIndex].name, layerName, StringComparison.Ordinal))
                    continue;
                ChildAnimatorState[] states = layers[layerIndex].stateMachine.states;
                for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
                    if (states[stateIndex].state != null &&
                        string.Equals(
                            states[stateIndex].state.name,
                            stateName,
                            StringComparison.Ordinal))
                    {
                        state = states[stateIndex].state;
                        return true;
                    }
            }
            return false;
        }

        private static bool MotionContains(
            Motion motion,
            AnimationClip clip,
            string directParameter,
            bool requireMirror)
        {
            if (motion == clip)
                return string.IsNullOrEmpty(directParameter) && !requireMirror;
            if (motion is not BlendTree tree) return false;
            ChildMotion[] children = tree.children;
            for (int index = 0; index < children.Length; index++)
            {
                ChildMotion child = children[index];
                if (!string.IsNullOrEmpty(directParameter) &&
                    !string.Equals(
                        child.directBlendParameter,
                        directParameter,
                        StringComparison.Ordinal))
                    continue;
                if (requireMirror && !child.mirror) continue;
                if (child.motion == clip) return true;
                if (string.IsNullOrEmpty(directParameter) &&
                    MotionContains(child.motion, clip, null, requireMirror))
                    return true;
            }
            return false;
        }
    }
}
