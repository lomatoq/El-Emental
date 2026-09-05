using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Samples the saved magic clips directly on the two production Humanoid
    /// avatars. This deliberately bypasses Animator state transitions, EAMM/C1,
    /// Animation Rigging and all native IK so a bad source/retarget interval can
    /// be distinguished from a presentation-graph fault.
    /// </summary>
    public static class EarthMagicSourceClipContinuityAudit
    {
        private const string PlayerRigPath =
            "Assets/Elemental/Content/Characters/Linebreaker/Linebreaker.fbx";
        private const string XBotRigPath = "Assets/ThirdParty/Mixamo/X Bot.fbx";
        private const string TreeName = "Earth Curated Casts";
        private const string ReportPath =
            "BuildReports/SeptemberAnimation/MagicSourceClipContinuity.json";
        private const float WindowStart = .15f;
        private const float WindowEnd = .35f;

        private static readonly HumanBodyBones[] UpperBodyBones =
        {
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand
        };

        [MenuItem("Elemental/QA/Audit Raw Magic Source Clip Continuity")]
        public static void Run()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null)
                throw new InvalidOperationException(
                    $"Missing controller: {EarthHumanoidMotionSetup.ControllerPath}");
            BlendTree tree = FindTree(controller, TreeName);
            if (tree == null || tree.blendType != BlendTreeType.Direct || tree.children.Length != 11)
                throw new InvalidOperationException(
                    $"{TreeName} must be the saved eleven-child direct BlendTree.");

            var report = new AuditReport
            {
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                controller = EarthHumanoidMotionSetup.ControllerPath,
                normalizedWindowStart = WindowStart,
                normalizedWindowEnd = WindowEnd,
                samples = new List<ClipSampleReport>(88)
            };
            string[] rigPaths = { PlayerRigPath, XBotRigPath };
            int[] sampleRates = { 30, 60 };
            ChildMotion[] children = tree.children;
            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                ChildMotion child = children[childIndex];
                if (!(child.motion is AnimationClip clip))
                    throw new InvalidOperationException(
                        $"Magic child {childIndex + 1} is not an AnimationClip.");
                int slot = ParseSlot(child.directBlendParameter, childIndex + 1);
                foreach (string rigPath in rigPaths)
                foreach (int sampleRate in sampleRates)
                {
                    report.samples.Add(Sample(
                        rigPath, clip, slot, sampleRate, SampleSpacing.SourceSeconds));
                    report.samples.Add(Sample(
                        rigPath, clip, slot, sampleRate, SampleSpacing.RuntimeClock));
                }
            }

            report.maximumNativeStepDegrees = Maximum(report.samples, SampleSpacing.SourceSeconds);
            report.maximumRuntimeClockStepDegrees = Maximum(report.samples, SampleSpacing.RuntimeClock);
            string absolute = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
            Debug.Log(
                $"[MagicSourceClipContinuity] nativeMax={report.maximumNativeStepDegrees:F3}deg " +
                $"runtimeClockMax={report.maximumRuntimeClockStepDegrees:F3}deg report={absolute}");
        }

        private static ClipSampleReport Sample(
            string rigPath,
            AnimationClip clip,
            int slot,
            int sampleRate,
            SampleSpacing spacing)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            if (prefab == null) throw new InvalidOperationException($"Missing Humanoid rig: {rigPath}");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman || animator.avatar == null ||
                    !animator.avatar.isValid)
                    throw new InvalidOperationException($"Invalid Humanoid Animator in {rigPath}");
                animator.enabled = false;
                var bones = new List<Transform>(UpperBodyBones.Length);
                var boneIds = new List<HumanBodyBones>(UpperBodyBones.Length);
                foreach (HumanBodyBones boneId in UpperBodyBones)
                {
                    Transform bone = animator.GetBoneTransform(boneId);
                    if (bone == null) continue;
                    bones.Add(bone);
                    boneIds.Add(boneId);
                }
                if (bones.Count < 9)
                    throw new InvalidOperationException(
                        $"{rigPath} exposes only {bones.Count} upper-body Humanoid bones.");

                float normalizedStep = spacing == SampleSpacing.SourceSeconds
                    ? 1f / (sampleRate * Mathf.Max(.001f, clip.length))
                    : EarthMagicClipClock.MaximumSpeedForSlot(slot) / sampleRate;
                normalizedStep = Mathf.Max(.00001f, normalizedStep);
                var previous = new Quaternion[bones.Count];
                var spikes = new List<BoneStepReport>();
                float normalized = WindowStart;
                clip.SampleAnimation(instance, normalized * clip.length);
                for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
                    previous[boneIndex] = bones[boneIndex].localRotation;

                float maximum = 0f;
                string maximumBone = string.Empty;
                float maximumFrom = normalized;
                float maximumTo = normalized;
                int steps = 0;
                while (normalized < WindowEnd - .000001f)
                {
                    float next = Mathf.Min(WindowEnd, normalized + normalizedStep);
                    clip.SampleAnimation(instance, next * clip.length);
                    for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
                    {
                        Quaternion current = bones[boneIndex].localRotation;
                        float angle = Quaternion.Angle(previous[boneIndex], current);
                        if (angle > maximum)
                        {
                            maximum = angle;
                            maximumBone = boneIds[boneIndex].ToString();
                            maximumFrom = normalized;
                            maximumTo = next;
                        }
                        if (angle >= 30f)
                            spikes.Add(new BoneStepReport
                            {
                                bone = boneIds[boneIndex].ToString(),
                                fromNormalized = normalized,
                                toNormalized = next,
                                sourceSeconds = (next - normalized) * clip.length,
                                angleDegrees = angle
                            });
                        previous[boneIndex] = current;
                    }
                    normalized = next;
                    steps++;
                }

                return new ClipSampleReport
                {
                    rig = rigPath,
                    slot = slot,
                    clip = clip.name,
                    clipPath = AssetDatabase.GetAssetPath(clip),
                    clipLengthSeconds = clip.length,
                    sampleRate = sampleRate,
                    spacing = spacing.ToString(),
                    normalizedStep = normalizedStep,
                    sourceSecondsPerStep = normalizedStep * clip.length,
                    stepCount = steps,
                    maximumStepDegrees = maximum,
                    maximumBone = maximumBone,
                    maximumFromNormalized = maximumFrom,
                    maximumToNormalized = maximumTo,
                    spikesOverThirtyDegrees = spikes.ToArray()
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static BlendTree FindTree(AnimatorController controller, string name)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(
                         AssetDatabase.GetAssetPath(controller)))
                if (asset is BlendTree tree && tree.name == name) return tree;
            return null;
        }

        private static int ParseSlot(string parameter, int fallback)
        {
            if (!string.IsNullOrEmpty(parameter) && parameter.Length >= 2 &&
                int.TryParse(parameter.Substring(parameter.Length - 2), out int slot) &&
                slot >= 1 && slot <= 11)
                return slot;
            return fallback;
        }

        private static float Maximum(List<ClipSampleReport> samples, SampleSpacing spacing)
        {
            float maximum = 0f;
            string label = spacing.ToString();
            foreach (ClipSampleReport sample in samples)
                if (sample.spacing == label)
                    maximum = Mathf.Max(maximum, sample.maximumStepDegrees);
            return maximum;
        }

        private enum SampleSpacing
        {
            SourceSeconds,
            RuntimeClock
        }

        [Serializable]
        private sealed class AuditReport
        {
            public string utc;
            public string controller;
            public float normalizedWindowStart;
            public float normalizedWindowEnd;
            public float maximumNativeStepDegrees;
            public float maximumRuntimeClockStepDegrees;
            public List<ClipSampleReport> samples;
        }

        [Serializable]
        private sealed class ClipSampleReport
        {
            public string rig;
            public int slot;
            public string clip;
            public string clipPath;
            public float clipLengthSeconds;
            public int sampleRate;
            public string spacing;
            public float normalizedStep;
            public float sourceSecondsPerStep;
            public int stepCount;
            public float maximumStepDegrees;
            public string maximumBone;
            public float maximumFromNormalized;
            public float maximumToNormalized;
            public BoneStepReport[] spikesOverThirtyDegrees;
        }

        [Serializable]
        private sealed class BoneStepReport
        {
            public string bone;
            public float fromNormalized;
            public float toNormalized;
            public float sourceSeconds;
            public float angleDegrees;
        }
    }
}
