using System;
using System.Collections.Generic;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Samples imported Humanoid motion and writes only importer-side custom
    /// curves. Source FBX payloads and discrete Animation Events are untouched.
    /// </summary>
    public static class EarthAnimationClipMetadataPipeline
    {
        private const int AnalysisSampleCount = 61;

        [MenuItem("Elemental Suite/Character/Analyze Controller Clip Metadata")]
        public static void AnalyzeControllerClipMetadata()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null)
                throw new InvalidOperationException(
                    $"AnimatorController is missing: {EarthHumanoidMotionSetup.ControllerPath}");

            var byPath = new Dictionary<string, List<AnimationClip>>(StringComparer.Ordinal);
            var seen = new HashSet<AnimationClip>();
            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                CollectStateMachineClips(layers[layerIndex].stateMachine, byPath, seen);

            int updatedClips = 0;
            foreach (KeyValuePair<string, List<AnimationClip>> entry in byPath)
                updatedClips += AnalyzeAndApplyImporter(entry.Key, entry.Value);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Elemental] Continuous animation metadata analyzed for {updatedClips} " +
                "controller clips. Source FBX files and Animation Events were not modified.");
        }

        public static bool HasRequiredCurves(AnimationClip clip)
        {
            if (clip == null) return false;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int curveIndex = 0; curveIndex < EarthAnimationClipMetadata.CurveCount; curveIndex++)
            {
                string required = EarthAnimationClipMetadata.CurveName(curveIndex);
                bool found = false;
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                    if (string.Equals(
                            bindings[bindingIndex].propertyName,
                            required,
                            StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                if (!found) return false;
            }
            return true;
        }

        public static EarthAnimationMetadataSample[] AnalyzeClipForCatalog(
            AnimationClip clip) =>
            AnalyzeClip(clip);

        public static EarthAnimationMetadataIssue ValidateClipMetadata(
            AnimationClip clip,
            bool locomotion,
            List<string> errors,
            string context)
        {
            if (clip == null)
            {
                errors?.Add($"Animation metadata clip is null: {context}.");
                return EarthAnimationMetadataIssue.MissingCurve;
            }

            var presence = new bool[EarthAnimationClipMetadata.CurveCount];
            var curves = new AnimationCurve[EarthAnimationClipMetadata.CurveCount];
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                EditorCurveBinding binding = bindings[bindingIndex];
                for (int curveIndex = 0; curveIndex < curves.Length; curveIndex++)
                {
                    if (!string.Equals(
                            binding.propertyName,
                            EarthAnimationClipMetadata.CurveName(curveIndex),
                            StringComparison.Ordinal))
                        continue;
                    presence[curveIndex] = true;
                    curves[curveIndex] = AnimationUtility.GetEditorCurve(clip, binding);
                    break;
                }
            }

            const int validationSamples = 33;
            var samples = new EarthAnimationMetadataSample[validationSamples];
            for (int sampleIndex = 0; sampleIndex < validationSamples; sampleIndex++)
            {
                float time01 = sampleIndex / (validationSamples - 1f);
                samples[sampleIndex] = new EarthAnimationMetadataSample(
                    time01,
                    EvaluateNormalized(curves[0], time01),
                    EvaluateNormalized(curves[1], time01),
                    EvaluateNormalized(curves[2], time01),
                    EvaluateNormalized(curves[3], time01),
                    EvaluateNormalized(curves[4], time01),
                    EvaluateNormalized(curves[5], time01),
                    EvaluateNormalized(curves[6], time01),
                    EvaluateNormalized(curves[7], time01));
            }

            EarthAnimationMetadataIssue issues = EarthAnimationClipMetadata.Validate(
                presence,
                samples,
                locomotion);
            if (issues == EarthAnimationMetadataIssue.None) return issues;
            if ((issues & EarthAnimationMetadataIssue.MissingCurve) != 0)
                errors?.Add($"Animation clip '{context}' is missing continuous contact metadata.");
            if ((issues & EarthAnimationMetadataIssue.InvalidRange) != 0 ||
                (issues & EarthAnimationMetadataIssue.NonFiniteValue) != 0)
                errors?.Add($"Animation clip '{context}' has non-finite or out-of-range metadata.");
            if ((issues & EarthAnimationMetadataIssue.OverlappingContacts) != 0)
                errors?.Add($"Locomotion clip '{context}' has an impossible prolonged double-contact window.");
            if ((issues & EarthAnimationMetadataIssue.NoSafeExit) != 0)
                errors?.Add($"Animation clip '{context}' has no safe CanExit window.");
            return issues;
        }

        private static int AnalyzeAndApplyImporter(
            string assetPath,
            IReadOnlyList<AnimationClip> referencedClips)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
                return 0;
            ModelImporterClipAnimation[] imported = importer.clipAnimations;
            if (imported == null || imported.Length == 0)
                imported = importer.defaultClipAnimations;
            if (imported == null || imported.Length == 0) return 0;

            int updated = 0;
            for (int importedIndex = 0; importedIndex < imported.Length; importedIndex++)
            {
                ModelImporterClipAnimation target = imported[importedIndex];
                AnimationClip source = FindClip(referencedClips, target.name);
                if (source == null && referencedClips.Count == 1)
                    source = referencedClips[0];
                if (source == null) continue;

                EarthAnimationMetadataSample[] proposal = AnalyzeClip(source);
                if (proposal.Length == 0)
                {
                    Debug.LogWarning(
                        $"[Elemental] Could not sample contact metadata for {assetPath}/{target.name}.");
                    continue;
                }
                // PlanetMotor owns world translation and rotation. Keep every
                // controller clip on the same extracted-root contract before
                // metadata is blended between sources.
                target.lockRootRotation = false;
                target.lockRootHeightY = false;
                target.lockRootPositionXZ = false;
                target.keepOriginalOrientation = true;
                target.keepOriginalPositionY = false;
                target.keepOriginalPositionXZ = true;
                target.heightFromFeet = true;
                target.curves = MergeCurves(target.curves, proposal);
                imported[importedIndex] = target;
                updated++;
            }
            if (updated == 0) return 0;
            importer.clipAnimations = imported;
            importer.SaveAndReimport();
            return updated;
        }

        private static EarthAnimationMetadataSample[] AnalyzeClip(AnimationClip clip)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EarthHumanoidMotionSetup.CanonicalCharacterPath);
            if (prefab == null || clip == null || clip.length <= 0.01f)
                return Array.Empty<EarthAnimationMetadataSample>();

            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                    return Array.Empty<EarthAnimationMetadataSample>();
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (leftFoot == null || rightFoot == null || pelvis == null)
                    return Array.Empty<EarthAnimationMetadataSample>();

                Transform root = instance.transform;
                var source = new EarthAnimationKinematicSample[AnalysisSampleCount];
                for (int index = 0; index < source.Length; index++)
                {
                    float time01 = index / (source.Length - 1f);
                    clip.SampleAnimation(instance, time01 * clip.length);
                    source[index] = new EarthAnimationKinematicSample(
                        time01,
                        ToFloat3(root.InverseTransformPoint(leftFoot.position)),
                        ToFloat3(root.InverseTransformPoint(rightFoot.position)),
                        ToFloat3(root.InverseTransformPoint(pelvis.position)),
                        ToFloat3(root.localPosition));
                }
                return EarthAnimationClipMetadata.Analyze(
                    source,
                    clip.isLooping,
                    IsLandingClip(clip.name));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static ClipAnimationInfoCurve[] MergeCurves(
            ClipAnimationInfoCurve[] existing,
            IReadOnlyList<EarthAnimationMetadataSample> proposal)
        {
            int existingCount = existing != null ? existing.Length : 0;
            var merged = new List<ClipAnimationInfoCurve>(
                existingCount + EarthAnimationClipMetadata.CurveCount);
            for (int index = 0; index < existingCount; index++)
                if (!string.IsNullOrWhiteSpace(existing[index].name) &&
                    existing[index].curve != null &&
                    existing[index].curve.length > 0 &&
                    !IsMetadataCurve(existing[index].name))
                    merged.Add(existing[index]);
            for (int curveIndex = 0; curveIndex < EarthAnimationClipMetadata.CurveCount; curveIndex++)
            {
                int keyCount = Mathf.Min(17, proposal.Count);
                var keys = new Keyframe[keyCount];
                for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
                {
                    int sampleIndex = keyCount > 1
                        ? Mathf.RoundToInt(keyIndex * (proposal.Count - 1f) / (keyCount - 1f))
                        : 0;
                    float time = proposal[sampleIndex].Time01;
                    float value = proposal[sampleIndex].CurveValue(curveIndex);
                    if (!float.IsFinite(time)) time = keyIndex / Mathf.Max(1f, keyCount - 1f);
                    if (!float.IsFinite(value)) value = 0f;
                    keys[keyIndex] = new Keyframe(Mathf.Clamp01(time), Mathf.Clamp01(value));
                }
                merged.Add(new ClipAnimationInfoCurve
                {
                    name = EarthAnimationClipMetadata.CurveName(curveIndex),
                    curve = new AnimationCurve(keys)
                });
            }
            return merged.ToArray();
        }

        private static void CollectStateMachineClips(
            AnimatorStateMachine machine,
            IDictionary<string, List<AnimationClip>> byPath,
            ISet<AnimationClip> seen)
        {
            if (machine == null) return;
            ChildAnimatorState[] states = machine.states;
            for (int index = 0; index < states.Length; index++)
                CollectMotion(states[index].state != null ? states[index].state.motion : null, byPath, seen);
            ChildAnimatorStateMachine[] children = machine.stateMachines;
            for (int index = 0; index < children.Length; index++)
                CollectStateMachineClips(children[index].stateMachine, byPath, seen);
        }

        private static void CollectMotion(
            Motion motion,
            IDictionary<string, List<AnimationClip>> byPath,
            ISet<AnimationClip> seen)
        {
            if (motion is AnimationClip clip)
            {
                if (!seen.Add(clip)) return;
                string path = AssetDatabase.GetAssetPath(clip);
                if (!byPath.TryGetValue(path, out List<AnimationClip> clips))
                {
                    clips = new List<AnimationClip>(4);
                    byPath.Add(path, clips);
                }
                clips.Add(clip);
                return;
            }
            if (motion is not BlendTree tree) return;
            ChildMotion[] children = tree.children;
            for (int index = 0; index < children.Length; index++)
                CollectMotion(children[index].motion, byPath, seen);
        }

        private static AnimationClip FindClip(IReadOnlyList<AnimationClip> clips, string name)
        {
            for (int index = 0; index < clips.Count; index++)
                if (clips[index] != null &&
                    string.Equals(clips[index].name, name, StringComparison.Ordinal))
                    return clips[index];
            return null;
        }

        private static bool IsMetadataCurve(string name)
        {
            for (int index = 0; index < EarthAnimationClipMetadata.CurveCount; index++)
                if (string.Equals(
                        name,
                        EarthAnimationClipMetadata.CurveName(index),
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsLandingClip(string name) =>
            !string.IsNullOrEmpty(name) &&
            (name.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("falling to roll", StringComparison.OrdinalIgnoreCase) >= 0);

        public static bool IsLocomotionClip(string name) =>
            !string.IsNullOrEmpty(name) &&
            (name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("locomotion", StringComparison.OrdinalIgnoreCase) >= 0);

        private static float EvaluateNormalized(AnimationCurve curve, float time01)
        {
            if (curve == null || curve.length == 0) return 0f;
            Keyframe last = curve.keys[curve.length - 1];
            float duration = Mathf.Max(0.0001f, last.time);
            return curve.Evaluate(Mathf.Clamp01(time01) * duration);
        }

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);
    }
}
