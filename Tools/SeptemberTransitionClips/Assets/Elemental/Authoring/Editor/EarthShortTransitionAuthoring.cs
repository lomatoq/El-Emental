using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    /// <summary>Imports exactly three new clips; never rebuilds an existing locomotion tree.</summary>
    public static class EarthShortTransitionAuthoring
    {
        private const string AssetRoot = "Assets/ThirdParty/Mixamo/";
        private static readonly string[] Files =
        { "X Bot@Start Walking.fbx", "X Bot@Crouch To Stand.fbx", "X Bot@Jumping.fbx" };

        [MenuItem("Elemental/Animation/Install Three Short Transitions")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing transition clips.");
            foreach (string file in Files)
                if (!File.Exists(AssetRoot + file) && !File.Exists("BuildReports/" + file))
                    throw new FileNotFoundException("Downloaded Mixamo transition missing: " + file);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Existing character controller is required.");
            var avatar = AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.CanonicalCharacterPath)
                .OfType<Avatar>().FirstOrDefault(value => value.isValid && value.isHuman);
            if (avatar == null) throw new InvalidOperationException("Canonical X Bot Humanoid Avatar is invalid.");
            var machine = controller.layers[0].stateMachine;
            // Direct motion references plus their serialized contents protect child trees,
            // thresholds, timescales, mirrored legs and controller exits against a rebuild.
            var existingMotions = new Dictionary<Motion, string>();
            foreach (var child in machine.states)
                if (!IsOurState(child.state.name)) Snapshot(child.state.motion, existingMotions);
            var report = new List<string>();
            for (int index = 0; index < Files.Length; index++)
            {
                var kind = (EarthShortTransition)(index + 1);
                string asset = AssetRoot + Files[index];
                if (!File.Exists(asset)) File.Copy("BuildReports/" + Files[index], asset, false);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("FBX did not produce ModelImporter: " + asset);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                HumanDescription human = importer.humanDescription;
                human.hasTranslationDoF = false;
                importer.humanDescription = human;
                var defaultClips = importer.defaultClipAnimations;
                if (defaultClips == null || defaultClips.Length == 0)
                    throw new InvalidOperationException("No source take in " + asset);
                var clipSettings = defaultClips[0];
                clipSettings.name = EarthShortTransitionPolicy.StateName(kind);
                ConfigureExtractedRoot(clipSettings);
                importer.clipAnimations = new[] { clipSettings };
                importer.SaveAndReimport();
                var full = LoadClip(asset);
                float onset = FindMotionOnset(full, kind);
                float duration = Mathf.Min(EarthShortTransitionPolicy.Duration(kind), full.length - onset);
                if (duration < .12f) throw new InvalidOperationException("Insufficient moving clip span in " + asset);
                float sourceFirst = clipSettings.firstFrame;
                clipSettings.firstFrame = sourceFirst + onset * full.frameRate;
                clipSettings.lastFrame = clipSettings.firstFrame + duration * full.frameRate;
                importer.clipAnimations = new[] { clipSettings };
                importer.SaveAndReimport();
                var clip = LoadClip(asset);
                clipSettings.curves = AnalyzeContactCurves(clip);
                importer.clipAnimations = new[] { clipSettings };
                importer.SaveAndReimport();
                clip = LoadClip(asset);
                if (!EarthAnimationClipMetadataPipeline.HasRequiredCurves(clip))
                    throw new InvalidOperationException("Contact metadata was not imported: " + asset);
                foreach (var parameterIndex in Enumerable.Range(0, EarthAnimationClipMetadata.CurveCount))
                {
                    string parameter = EarthAnimationClipMetadata.CurveName(parameterIndex);
                    if (!controller.parameters.Any(value => value.name == parameter))
                        controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
                }
                string stateName = EarthShortTransitionPolicy.StateName(kind);
                var state = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == stateName)
                    ?? machine.AddState(stateName);
                state.motion = clip;
                state.speed = 1f;
                state.speedParameterActive = false;
                state.timeParameterActive = false;
                state.iKOnFeet = true;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
                report.Add($"{Files[index]} -> {stateName}: onset {onset:F3}s, duration {clip.length:F3}s, frames {clipSettings.firstFrame:F2}..{clipSettings.lastFrame:F2}; eight sampled contact curves; root tracks extracted.");
            }
            foreach (var entry in existingMotions)
                if (EditorJsonUtility.ToJson(entry.Key) != entry.Value)
                    throw new InvalidOperationException("Existing user motion was unexpectedly modified: " + entry.Key.name);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("BuildReports/ShortTransitions");
            File.WriteAllLines("BuildReports/ShortTransitions/import-report.txt", report);
            Debug.Log("[Elemental] Installed three incremental transition states; existing locomotion motion graphs are byte-identical. Runtime selects bounded bridge slots through EarthShortTransitionPolicy.");
        }

        private static bool IsOurState(string name) => Enumerable.Range(1, 3)
            .Any(value => name == EarthShortTransitionPolicy.StateName((EarthShortTransition)value));

        private static void Snapshot(Motion motion, Dictionary<Motion, string> target)
        {
            if (motion == null || target.ContainsKey(motion)) return;
            target.Add(motion, EditorJsonUtility.ToJson(motion));
            if (motion is BlendTree tree)
                foreach (var child in tree.children) Snapshot(child.motion, target);
        }

        private static void ConfigureExtractedRoot(ModelImporterClipAnimation clip)
        {
            clip.loopTime = clip.loopPose = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = false;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = false;
            clip.keepOriginalPositionXZ = true;
            clip.heightFromFeet = true;
        }

        private static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().First(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal));

        private static float FindMotionOnset(AnimationClip clip, EarthShortTransition kind)
        {
            var samples = Sample(clip);
            if (kind == EarthShortTransition.CrouchExit)
            {
                float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                int lowestIndex = 0;
                for (int index = 0; index < samples.Length; index++)
                {
                    float height = samples[index].PelvisPosition.y;
                    if (height < minimum) { minimum = height; lowestIndex = index; }
                    maximum = Mathf.Max(maximum, height);
                }
                float rise = maximum - minimum;
                if (!float.IsFinite(rise) || rise < .06f)
                    throw new InvalidOperationException("Crouch exit must contain at least 6cm of measured pelvis rise: " + clip.name);
                // The shipping pillar pose is a half-crouch. Starting at the first
                // movement of the source's full squat made release dip farther down.
                // Enter at 40% of its measured rise, retaining original playback speed.
                float halfCrouchHeight = minimum + rise * .4f;
                for (int index = lowestIndex + 1; index < samples.Length - 2; index++)
                    if (samples[index].PelvisPosition.y >= halfCrouchHeight &&
                        samples[index].PelvisPosition.y > samples[index - 1].PelvisPosition.y)
                        return samples[index].Time01 * clip.length;
                throw new InvalidOperationException("Crouch exit never reaches the measured half-crouch height on an ascending span: " + clip.name);
            }
            for (int index = 1; index < samples.Length - 2; index++)
            {
                float motion = math.max(math.distance(samples[index].LeftFootPosition, samples[0].LeftFootPosition),
                    math.distance(samples[index].RightFootPosition, samples[0].RightFootPosition));
                if (motion >= .025f)
                    return Mathf.Max(0f, samples[index].Time01 * clip.length - .05f);
            }
            throw new InvalidOperationException("Downloaded transition contains no detectable movement: " + clip.name);
        }

        private static ClipAnimationInfoCurve[] AnalyzeContactCurves(AnimationClip clip)
        {
            var metadata = EarthAnimationClipMetadata.Analyze(Sample(clip), false, false);
            var curves = new ClipAnimationInfoCurve[EarthAnimationClipMetadata.CurveCount];
            for (int curve = 0; curve < curves.Length; curve++)
            {
                var keys = new Keyframe[metadata.Length];
                for (int sample = 0; sample < keys.Length; sample++)
                    keys[sample] = new Keyframe(metadata[sample].Time01, metadata[sample].CurveValue(curve));
                curves[curve] = new ClipAnimationInfoCurve
                { name = EarthAnimationClipMetadata.CurveName(curve), curve = new AnimationCurve(keys) };
            }
            return curves;
        }

        private static EarthAnimationKinematicSample[] Sample(AnimationClip clip)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EarthHumanoidMotionSetup.CanonicalCharacterPath);
            var instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                animator.applyRootMotion = false;
                var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var root = instance.transform;
                int count = Mathf.Clamp(Mathf.CeilToInt(clip.length * 60f) + 1, 3, 601);
                var samples = new EarthAnimationKinematicSample[count];
                for (int index = 0; index < count; index++)
                {
                    float time = index / (count - 1f);
                    clip.SampleAnimation(instance, time * clip.length);
                    samples[index] = new EarthAnimationKinematicSample(time,
                        ToFloat3(root.InverseTransformPoint(left.position)), ToFloat3(root.InverseTransformPoint(right.position)),
                        ToFloat3(root.InverseTransformPoint(hips.position)), ToFloat3(root.localPosition));
                }
                return samples;
            }
            finally { Object.DestroyImmediate(instance); }
        }
        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
