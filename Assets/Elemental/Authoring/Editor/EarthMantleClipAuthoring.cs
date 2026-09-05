using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Explicit prototype authoring, not an imported Mixamo performance. The motor moves
    /// the root; this clip poses limbs. No startup hook and no production-scene writes.
    /// </summary>
    public static class EarthMantleClipAuthoring
    {
        public const string ClipPath = "Assets/Elemental/Content/Animation/Earth Authored Mantle Prototype.anim";
        private const float Duration = 1.2f;
        private static readonly float[] Times = { 0f, .12f, .30f, .60f, .76f, .90f, .94f, 1f };
        private static readonly float[] Reach = { 0f, .9f, 1f, 1f, .55f, .08f, 0f, 0f };
        private static readonly float[] LeftLift = { 0f, 0f, .45f, 1f, .5f, .18f, .06f, 0f };
        private static readonly float[] RightLift = { 0f, 0f, .2f, 1f, .8f, .25f, .06f, 0f };

        [MenuItem("Elemental/Character/Create Authored Mantle Prototype")]
        public static void CreateAndBind()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create mantle content in Edit Mode.");
            if (AssetDatabase.LoadMainAssetAtPath(ClipPath) != null)
                throw new InvalidOperationException("An authored mantle asset already exists. Review it; this command never overwrites authored content.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Missing Earth animation controller.");
            AnimatorState state = null;
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.name == "Mantle") state = child.state;
            if (state != null && state.motion != null && state.tag != "MantleFallback")
                throw new InvalidOperationException("Mantle already has authored content. Existing content was preserved.");

            AnimationClip idle = LoadIdle();
            // Animation-only FBX files use CopyFromOther and need not contain an
            // Avatar on their prefab. Sample on the actual shared Avatar's own
            // skeleton, rather than attaching it to an unverified clip hierarchy.
            var idleImporter = AssetImporter.GetAtPath(EarthHumanoidMotionSetup.IdlePath) as ModelImporter;
            Avatar samplingAvatar = idleImporter != null ? idleImporter.sourceAvatar : null;
            if (samplingAvatar == null)
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.CanonicalCharacterPath))
                    if (asset is Avatar avatar && avatar.isValid && avatar.isHuman) { samplingAvatar = avatar; break; }
            if (samplingAvatar == null || !samplingAvatar.isValid || !samplingAvatar.isHuman)
                throw new InvalidOperationException("Idle's shared canonical Humanoid Avatar is missing or invalid.");
            string samplingModelPath = AssetDatabase.GetAssetPath(samplingAvatar);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(samplingModelPath);
            if (model == null) throw new InvalidOperationException("Shared Avatar must have its source model: " + samplingModelPath);
            GameObject sample = null;
            AnimationClip clip = null;
            try
            {
                sample = UnityEngine.Object.Instantiate(model);
                sample.name = "Mantle authoring sampler (temporary)";
                sample.hideFlags = HideFlags.HideAndDontSave;
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = sample.GetComponent<Animator>();
                if (animator == null) animator = sample.AddComponent<Animator>();
                animator.avatar = samplingAvatar;
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                animator.Rebind();
                idle.SampleAnimation(sample, 0f);
                using var handler = new HumanPoseHandler(animator.avatar, sample.transform);
                HumanPose basis = new HumanPose(); handler.GetHumanPose(ref basis);
                var poses = new List<float[]>();
                Vector3 leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                Vector3 rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                Vector3 leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
                Vector3 rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position;
                Vector3 leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Vector3 rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                Vector3 leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                Vector3 rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                float scale = animator.humanScale;
                for (int key = 0; key < Times.Length; key++)
                {
                    HumanPose pose = basis; pose.muscles = (float[])basis.muscles.Clone();
                    handler.SetHumanPose(ref pose);
                    // Fit named muscle channels against actual avatar geometry. This avoids
                    // guessing the sign of mirrored shoulder and hip muscle coordinates.
                    float push = Mathf.InverseLerp(.12f, .60f, Times[key]);
                    Vector3 armOffset = (Vector3.forward * .38f - Vector3.up * (.15f + push * .5f)) * scale;
                    Fit(handler, ref pose, animator, "Left", true,
                        Vector3.Lerp(leftHand, leftShoulder + armOffset, Reach[key]), Vector3.zero);
                    Fit(handler, ref pose, animator, "Right", true,
                        Vector3.Lerp(rightHand, rightShoulder + armOffset, Reach[key]), Vector3.zero);
                    Fit(handler, ref pose, animator, "Left", false,
                        leftFoot + (Vector3.up * .38f + Vector3.forward * .22f) * (LeftLift[key] * scale),
                        leftKnee + (Vector3.up * .18f + Vector3.forward * .28f) * (LeftLift[key] * scale));
                    Fit(handler, ref pose, animator, "Right", false,
                        rightFoot + (Vector3.up * .38f + Vector3.forward * .22f) * (RightLift[key] * scale),
                        rightKnee + (Vector3.up * .18f + Vector3.forward * .28f) * (RightLift[key] * scale));
                    // Exact upright endpoints; spine/neck/head and body position remain
                    // the sampled idle at every key, so no compensating torso compression.
                    if (key == 0 || key == Times.Length - 1) pose.muscles = (float[])basis.muscles.Clone();
                    poses.Add((float[])pose.muscles.Clone());
                }

                // Clone the real Humanoid clip to retain its avatar motion metadata.
                // Flatten every imported channel, including body/root translation and
                // rotation, then replace only named muscle channels with authored keys.
                clip = UnityEngine.Object.Instantiate(idle);
                clip.name = "Earth Authored Mantle Prototype"; clip.frameRate = 60f;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
                    if (source == null) continue;
                    AnimationUtility.SetEditorCurve(clip, binding,
                        AnimationCurve.Constant(0f, Duration, source.Evaluate(0f)));
                }
                string[] muscleNames = HumanTrait.MuscleName;
                for (int muscle = 0; muscle < muscleNames.Length; muscle++)
                {
                    // Finger channels have different Animator binding names; retain
                    // their frozen idle bindings rather than creating unknown curves.
                    if (HumanTrait.BoneFromMuscle(muscle) >= (int)HumanBodyBones.LeftThumbProximal) continue;
                    var keys = new Keyframe[Times.Length];
                    for (int key = 0; key < keys.Length; key++)
                        keys[key] = new Keyframe(Times[key] * Duration, poses[key][muscle], 0f, 0f);
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve("", typeof(Animator), muscleNames[muscle]), new AnimationCurve(keys));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0f; settings.stopTime = Duration;
                settings.loopTime = false; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.EnsureQuaternionContinuity();
                if (!clip.isHumanMotion) throw new InvalidOperationException("Generated clip lost Humanoid motion metadata.");
                ValidateStationaryRoot(clip);
                WriteSampleReport(clip, sample, animator);
                AssetDatabase.CreateAsset(clip, ClipPath);
                bool hasTime = false;
                foreach (var parameter in controller.parameters) if (parameter.name == "MantleTime") hasTime = true;
                if (!hasTime) controller.AddParameter("MantleTime", AnimatorControllerParameterType.Float);
                if (state == null) state = controller.layers[0].stateMachine.AddState("Mantle");
                state.motion = clip; state.tag = "AuthoredMantlePrototype";
                state.timeParameterActive = true; state.timeParameter = "MantleTime";
                state.transitions = Array.Empty<AnimatorStateTransition>();
                EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log("[Mantle Authoring] Created original keyframed Humanoid prototype, not Mixamo. Reach .00-.12, raise .12-.60, transfer .60-.94, settle .94-1. Motor remains root owner. Visual review at low/high ledges remains required: " + ClipPath);
            }
            finally
            {
                if (sample != null) UnityEngine.Object.DestroyImmediate(sample);
                if (clip != null && !AssetDatabase.Contains(clip)) UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AnimationClip LoadIdle()
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.IdlePath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal) && clip.isHumanMotion)
                    return clip;
            throw new InvalidOperationException("Repair/import upright Idle before generating mantle.");
        }

        private static void Fit(HumanPoseHandler handler, ref HumanPose pose, Animator animator, string side,
            bool arm, Vector3 target, Vector3 kneeTarget)
        {
            bool left = side == "Left";
            Transform end = animator.GetBoneTransform(arm ? (left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) :
                (left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot));
            Transform knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            string[] names = arm ? new[] { "Arm Down-Up", "Arm Front-Back", "Forearm Stretch" } :
                new[] { "Upper Leg Front-Back", "Lower Leg Stretch", "Foot Up-Down" };
            int[] channels = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                channels[i] = Array.IndexOf(HumanTrait.MuscleName, side + " " + names[i]);
                if (channels[i] < 0) throw new InvalidOperationException("Unknown Humanoid muscle: " + side + " " + names[i]);
            }
            // Small offline coordinate descent, bounded to legal muscles. No runtime
            // transforms or root warps; only these three channels can change per limb.
            foreach (float step in new[] { .28f, .14f, .07f, .035f, .0175f })
                for (int pass = 0; pass < 3; pass++)
                    foreach (int channel in channels)
                    {
                        handler.SetHumanPose(ref pose);
                        float initial = pose.muscles[channel], best = initial;
                        float error = Error(end, knee, target, kneeTarget, arm);
                        foreach (float sign in new[] { -1f, 1f })
                        {
                            pose.muscles[channel] = Mathf.Clamp(initial + sign * step, -.92f, .92f);
                            handler.SetHumanPose(ref pose);
                            float candidate = Error(end, knee, target, kneeTarget, arm);
                            if (candidate < error) { error = candidate; best = pose.muscles[channel]; }
                        }
                        pose.muscles[channel] = best;
                    }
            handler.SetHumanPose(ref pose);
        }

        private static float Error(Transform end, Transform knee, Vector3 target, Vector3 kneeTarget, bool arm) =>
            (end.position - target).sqrMagnitude + (arm ? 0f : .45f * (knee.position - kneeTarget).sqrMagnitude);

        private static void WriteSampleReport(AnimationClip clip, GameObject sample, Animator animator)
        {
            var report = new SampleReport { clip = ClipPath, samples = new PoseSample[Times.Length] };
            for (int key = 0; key < Times.Length; key++)
            {
                clip.SampleAnimation(sample, Times[key] * Duration);
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 up = sample.transform.up, forward = sample.transform.forward;
                report.samples[key] = new PoseSample
                {
                    progress = Times[key],
                    headAboveHips = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.Head).position - hips, up),
                    leftFootHeight = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position - sample.transform.position, up),
                    rightFootHeight = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightFoot).position - sample.transform.position, up),
                    rightHandForward = Vector3.Dot(animator.GetBoneTransform(HumanBodyBones.RightHand).position - hips, forward)
                };
                if (report.samples[key].headAboveHips < .25f * animator.humanScale)
                    throw new InvalidOperationException("Generated mantle compressed the upper body at progress " + Times[key]);
            }
            Directory.CreateDirectory("BuildReports/SeptemberAnimation");
            File.WriteAllText("BuildReports/SeptemberAnimation/MantleAuthoredClipSamples.json", JsonUtility.ToJson(report, true));
        }

        [Serializable] private sealed class SampleReport { public string clip; public PoseSample[] samples; }
        [Serializable] private struct PoseSample
        {
            public float progress, headAboveHips, leftFootHeight, rightFootHeight, rightHandForward;
        }

        private static void ValidateStationaryRoot(AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!binding.propertyName.StartsWith("RootT.", StringComparison.Ordinal) &&
                    !binding.propertyName.StartsWith("RootQ.", StringComparison.Ordinal) &&
                    binding.type != typeof(Transform)) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                float start = curve.Evaluate(0f);
                for (int i = 1; i <= 20; i++)
                    if (Mathf.Abs(curve.Evaluate(Duration * i / 20f) - start) > .00001f)
                        throw new InvalidOperationException("Mantle must not animate root or raw bone transforms: " + binding.propertyName);
            }
        }
    }
}
