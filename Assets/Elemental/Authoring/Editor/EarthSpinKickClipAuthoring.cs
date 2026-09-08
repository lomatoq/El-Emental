using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>Offline authored variation of the licensed local MMA performance, not a new mocap claim.</summary>
    public static class EarthSpinKickClipAuthoring
    {
        public const string ClipPath = "Assets/Elemental/Content/Animation/XBot Earth Spin Kick.anim";
        public const string KickClipPath = "Assets/Elemental/Content/Animation/XBot Earth Kick.anim";
        public const float Duration = 1.1f;
        public const float Contact = .6f;
        private const int Samples = 67;

        [MenuItem("Elemental/Character/Bake Earth Spin Kick")]
        public static void Bake()
        {
            BakeClip(KickClipPath, .78f, .5f, false);
            BakeClip(ClipPath, Duration, Contact, true);
        }

        private static void BakeClip(string outputPath, float duration, float contact, bool spin)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake the spin kick in Edit Mode.");
            AnimationClip source = LoadSource();
            Avatar avatar = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.CanonicalCharacterPath))
                if (asset is Avatar candidate && candidate.isHuman && candidate.isValid) { avatar = candidate; break; }
            if (avatar == null) throw new InvalidOperationException("The canonical X Bot Humanoid Avatar is unavailable.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EarthHumanoidMotionSetup.CanonicalCharacterPath);
            GameObject sample = UnityEngine.Object.Instantiate(prefab);
            sample.hideFlags = HideFlags.HideAndDontSave;
            AnimationClip baked = null;
            try
            {
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = sample.GetComponent<Animator>();
                if (animator == null) animator = sample.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.Rebind();
                float sourceContact = FindFootContact(source, sample, animator, out bool mirrorSource);
                baked = UnityEngine.Object.Instantiate(source);
                baked.name = spin ? "XBot Earth Spin Kick" : "XBot Earth Kick";
                baked.frameRate = 60f;
                AnimationUtility.SetAnimationEvents(baked, Array.Empty<AnimationEvent>());
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
                foreach (EditorCurveBinding binding in bindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                    var keys = new Keyframe[Samples];
                    for (int i = 0; i < Samples; i++)
                    {
                        float phase = i / (Samples - 1f);
                        float sourceTime = SourceTime(phase, contact, sourceContact, source.length);
                        float value = curve.Evaluate(sourceTime);
                        string property = binding.propertyName;
                        // Preserve the source's authored limbs and arm counterbalance. Trajectory
                        // belongs to PlanetMotor; only the human body quaternion gets the turn.
                        if (property == "RootT.x" || property == "RootT.z" ||
                            property.StartsWith("MotionT.", StringComparison.Ordinal) ||
                            property.StartsWith("MotionQ.", StringComparison.Ordinal)) value = curve.Evaluate(0f);
                        if (property == "RootT.y")
                            value = curve.Evaluate(0f) + Mathf.Clamp(value - curve.Evaluate(0f), -.045f, .045f);
                        keys[i] = new Keyframe(phase * duration, value);
                    }
                    AnimationUtility.SetEditorCurve(baked, binding, LinearCurve(keys));
                }
                AnimationCurve[] sourceRotation = new AnimationCurve[4];
                string[] axes = { "x", "y", "z", "w" };
                for (int axis = 0; axis < 4; axis++)
                {
                    sourceRotation[axis] = AnimationUtility.GetEditorCurve(source,
                        EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[axis]));
                    if (sourceRotation[axis] == null)
                        throw new InvalidOperationException("MMA Humanoid body rotation binding RootQ." + axes[axis] + " is missing.");
                }
                var rotations = new Quaternion[Samples];
                for (int i = 0; i < Samples; i++)
                {
                    float phase = i / (Samples - 1f);
                    float sourceTime = SourceTime(phase, contact, sourceContact, source.length);
                    Quaternion original = new Quaternion(sourceRotation[0].Evaluate(sourceTime),
                        sourceRotation[1].Evaluate(sourceTime), sourceRotation[2].Evaluate(sourceTime),
                        sourceRotation[3].Evaluate(sourceTime)).normalized;
                    // The full source also turns during recovery. The finisher owns
                    // one revolution, so retain source tilt but do not add two turns.
                    Vector3 sourceHeading = Vector3.ProjectOnPlane(original * Vector3.forward, Vector3.up);
                    Quaternion sourceYaw = sourceHeading.sqrMagnitude > .001f
                        ? Quaternion.LookRotation(sourceHeading.normalized, Vector3.up) : Quaternion.identity;
                    Quaternion rotated = spin
                        ? Quaternion.AngleAxis(SpinYaw(phase), Vector3.up) * Quaternion.Inverse(sourceYaw) * original
                        : original;
                    if (i > 0 && Quaternion.Dot(rotations[i - 1], rotated) < 0f)
                        rotated = new Quaternion(-rotated.x, -rotated.y, -rotated.z, -rotated.w);
                    rotations[i] = rotated;
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    var keys = new Keyframe[Samples];
                    for (int i = 0; i < Samples; i++)
                        keys[i] = new Keyframe(i * duration / (Samples - 1), rotations[i][axis]);
                    AnimationUtility.SetEditorCurve(baked,
                        EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[axis]), LinearCurve(keys));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(baked);
                settings.startTime = 0f;
                settings.stopTime = duration;
                settings.loopTime = false;
                settings.loopBlend = false;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionXZ = true;
                settings.loopBlendPositionY = true;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionXZ = true;
                settings.keepOriginalPositionY = true;
                // Clip-level Humanoid mirror includes bilateral muscle remapping and
                // reflected body rotation; do not guess signs of shoulder channels.
                settings.mirror = mirrorSource;
                AnimationUtility.SetAnimationClipSettings(baked, settings);
                baked.EnsureQuaternionContinuity();
                string report = ValidateSampledPerformance(baked, sample, animator, sourceContact, duration, contact, spin);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                if (existing == null) { AssetDatabase.CreateAsset(baked, outputPath); baked = null; }
                else EditorUtility.CopySerialized(baked, existing);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("BuildReports/EarthSpinKick");
                File.WriteAllText(spin ? "BuildReports/EarthSpinKick/BakeLatest.txt" : "BuildReports/EarthSpinKick/KickBakeLatest.txt", report);
                Debug.Log(report);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
                if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        public static float SpinYaw(float phase)
        {
            // Coil briefly, turn while chambered, face the aim again at foot contact.
            float t = Mathf.InverseLerp(.10f, Contact, phase);
            return 360f * (t * t * (3f - 2f * t));
        }

        public static float SourceTime(float phase, float contact, float sourceContact, float sourceDuration)
        {
            // Preserve the complete authored extension and recovery, never play
            // a cropped preparation backwards as a substitute for the kick.
            float t = phase <= contact ? phase / contact : (phase - contact) / (1f - contact);
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t);
            return phase <= contact ? sourceContact * t : Mathf.Lerp(sourceContact, sourceDuration, t);
        }

        private static AnimationCurve LinearCurve(Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        private static AnimationClip LoadSource()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(EarthHumanoidMotionSetup.MmaKickPath))
                if (asset is AnimationClip clip && clip.isHumanMotion && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    return clip;
            throw new InvalidOperationException("Import X Bot@Mma Kick.fbx as a valid Humanoid before baking the finisher.");
        }

        private static float FindFootContact(AnimationClip clip, GameObject sample, Animator animator, out bool mirrorSource)
        {
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            float bestRight = float.NegativeInfinity, bestLeft = float.NegativeInfinity;
            float rightTime = 0f, leftTime = 0f;
            for (int i = 0; i <= 120; i++)
            {
                float time = clip.length * i / 120f;
                clip.SampleAnimation(sample, time);
                float scoreRight = right.position.y - left.position.y;
                float scoreLeft = -scoreRight;
                if (scoreRight > bestRight) { bestRight = scoreRight; rightTime = time; }
                if (scoreLeft > bestLeft) { bestLeft = scoreLeft; leftTime = time; }
            }
            mirrorSource = bestLeft > bestRight;
            float contact = mirrorSource ? leftTime : rightTime;
            string diagnostic = $"MMA source sampling: right lift={bestRight:F4}m at {rightTime:F3}s, left lift={bestLeft:F4}m at {leftTime:F3}s; clip={clip.length:F3}s, humanScale={animator.humanScale:F4}, active={sample.activeInHierarchy}, mirrorToRight={mirrorSource}.";
            Debug.Log(diagnostic);
            Directory.CreateDirectory("BuildReports/EarthSpinKick");
            File.WriteAllText("BuildReports/EarthSpinKick/SourceSampleLatest.txt", diagnostic);
            if (Mathf.Max(bestRight, bestLeft) < .1f || contact <= .05f)
                throw new InvalidOperationException("Source has no distinct sampled kick contact: " + diagnostic);
            return contact;
        }

        private static string ValidateSampledPerformance(AnimationClip clip, GameObject sample, Animator animator, float sourceContact, float duration, float contact, bool spin)
        {
            if (!clip.isHumanMotion) throw new InvalidOperationException("Baked finisher lost Humanoid metadata.");
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            float turn = 0f, rootDrift = 0f;
            Vector3 previous = Vector3.zero;
            Vector3 minimum = Vector3.positiveInfinity, maximum = Vector3.negativeInfinity;
            for (int i = 0; i < Samples; i++)
            {
                sample.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clip.SampleAnimation(sample, duration * i / (Samples - 1));
                rootDrift = Mathf.Max(rootDrift, sample.transform.position.magnitude);
                Vector3 heading = Vector3.ProjectOnPlane(hips.forward, Vector3.up).normalized;
                if (i > 0) turn += Vector3.SignedAngle(previous, heading, Vector3.up);
                previous = heading;
                minimum = Vector3.Min(minimum, right.position);
                maximum = Vector3.Max(maximum, right.position);
            }
            clip.SampleAnimation(sample, duration * contact);
            float contactLift = right.position.y - left.position.y;
            if ((spin && Mathf.Abs(turn) < 300f) || rootDrift > .01f || (maximum - minimum).magnitude < .4f || contactLift < .1f)
                throw new InvalidOperationException($"Spin bake failed physical pose checks: turn={turn:F1}°, rootDrift={rootDrift:F4}m, footRange={(maximum-minimum).magnitude:F3}m, right contact lift={contactLift:F3}m. Check RootQ pose import and Humanoid mirror before shipping.");
            return $"Earth spin kick baked from local MMA source. Duration {duration:F2}s, contact {contact:F2}, sourceContact={sourceContact:F3}s, sampled hip turn={turn:F1}°, rootDrift={rootDrift:F4}m, rightFootRange={(maximum-minimum).magnitude:F3}m, right contact lift={contactLift:F3}m. Requires full Humanoid body/root mask and applyRootMotion=false; runtime contact and visual acceptance remain required.";
        }
    }
}
