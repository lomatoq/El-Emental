using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Authoring;
using Elemental.Presentation.MotionMatching;
using MotionMatching;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using AnimationClip = UnityEngine.AnimationClip;

namespace Elemental.Authoring.Editor.MotionMatching
{
    public static class MotionLibraryBuilder
    {
        private const string OutputFolder = "Assets/Elemental/Content/Characters/MotionMatching";
        public const string RetargetBindPosePath =
            OutputFolder + "/EarthRetargetBindPose.asset";
        private static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.Hips,
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
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
            HumanBodyBones.RightToes
        };

        [MenuItem("Elemental Suite/Character/Bake Selected EAMM Motion Library")]
        public static void BakeSelected()
        {
            MotionLibraryAsset library = Selection.activeObject as MotionLibraryAsset;
            if (library == null)
                throw new InvalidOperationException("Select a MotionLibraryAsset before baking.");
            Bake(library);
        }

        [MenuItem("Elemental Suite/Character/Bake Selected EAMM Motion Library", true)]
        private static bool CanBakeSelected() => Selection.activeObject is MotionLibraryAsset;

        public static MotionMatchingData Bake(MotionLibraryAsset library)
        {
            ValidateOrThrow(library);
            string libraryAssetPath = AssetDatabase.GetAssetPath(library);
            string libraryName = library.name;
            EnsureFolder(OutputFolder);
            string dataName = libraryName.EndsWith("Data", StringComparison.Ordinal)
                ? libraryName
                : libraryName + "Data";
            string assetPath = $"{OutputFolder}/{dataName}.asset";
            if (string.Equals(assetPath, libraryAssetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Motion database output must not overwrite its MotionLibraryAsset.");
            MotionMatchingData data = AssetDatabase.LoadAssetAtPath<MotionMatchingData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MotionMatchingData>();
                data.name = dataName;
                AssetDatabase.CreateAsset(data, assetPath);
            }
            else if (!string.Equals(data.name, dataName, StringComparison.Ordinal))
            {
                data.name = dataName;
            }

            // Creating/importing the output asset can invalidate native Unity
            // object wrappers. Continue from a fresh serialized library handle.
            library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(libraryAssetPath);
            if (library == null)
                throw new InvalidOperationException($"Motion library could not be reloaded after output creation: {libraryAssetPath}");

            ConfigureFeatures(data);
            GameObject instance = UnityEngine.Object.Instantiate(library.sourceRig);
            instance.name = $"{libraryName}_BakeRig";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Motion library source rig must contain a valid Humanoid Animator.");
                animator.enabled = false;
                WriteRetargetBindPose(animator);
                Transform restHips = animator.GetBoneTransform(HumanBodyBones.Hips);
                data.HipsUpLocalVector = restHips.InverseTransformDirection(animator.transform.up).normalized;
                data.HipsForwardLocalVector = restHips.InverseTransformDirection(animator.transform.forward).normalized;

                List<Transform> transforms = BuildTransformList(animator, data, out Skeleton skeleton);
                var poseSet = new PoseSet(data);
                poseSet.SetSkeletonFromFile(skeleton);
                float frameTime = 1f / Mathf.Max(1f, library.databaseRate);
                foreach (MotionClipRecipe recipe in library.clips)
                {
                    if (!IsSearchableBaseMotion(recipe.role)) continue;
                    PoseVector[] poses = SampleClip(instance, animator, transforms, recipe, frameTime);
                    if (recipe.role == MotionClipRole.Locomotion &&
                        !HasBilateralFootCycle(poses, out string contactSummary))
                        throw new InvalidOperationException(
                            $"{recipe.stableId}/{recipe.clip.name} has an incomplete foot-contact cycle " +
                            $"({contactSummary}). Directional locomotion requires plant and swing samples " +
                            "for both feet.");
                    poseSet.AddClip(poses, frameTime, out int animationClip);
                    string queryTag = ResolveQueryTag(recipe.role, recipe.semantic);
                    poseSet.AddTag(animationClip, new AnimationData.Tag
                    {
                        Name = queryTag,
                        Start = new[] { 0 },
                        End = new[] { poses.Length }
                    });
                }
                poseSet.ConvertTagsToNativeArrays();

                float3[] forwardAxes = new float3[skeleton.Joints.Count];
                for (int i = 0; i < forwardAxes.Length; i++) forwardAxes[i] = math.forward();
                data.SetJointsLocalForward(forwardAxes);
                var features = new FeatureSet(data, poseSet.NumberPoses);
                features.Extract(poseSet, data);
                features.NormalizeFeatures();
                new PoseSerializer().Serialize(poseSet, data.GetAssetPath(), data.name);
                new FeatureSerializer().Serialize(features, data, data.GetAssetPath(), data.name);
                features.Dispose();
                poseSet.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            EditorUtility.SetDirty(data);
            MigrateLegacySearchAsset(library.name, dataName);
            EnsureEnvironmentSearch(dataName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EAMM] Baked {library.clips.Count} provenance-safe clips to {assetPath} at {library.databaseRate:0.#} Hz.");
            return data;
        }

        public static EarthRetargetBindPose BakeRetargetBindPose(MotionLibraryAsset library)
        {
            if (library == null || library.sourceRig == null)
                throw new InvalidOperationException("A source rig is required to bake retarget metadata.");
            EnsureFolder(OutputFolder);
            GameObject instance = UnityEngine.Object.Instantiate(library.sourceRig);
            instance.name = $"{library.name}_BindPoseRig";
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException(
                        "Motion library source rig must contain a valid Humanoid Animator.");
                animator.enabled = false;
                EarthRetargetBindPose result = WriteRetargetBindPose(animator);
                AssetDatabase.SaveAssets();
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static EarthRetargetBindPose WriteRetargetBindPose(Animator animator)
        {
            List<EarthRetargetBindBone> entries = CaptureRetargetBindBones(animator);
            if (!ContainsRequiredRetargetBones(entries))
                throw new InvalidOperationException(
                    "Source rig is missing one or more required hips/spine/arm/leg/foot bind bones.");

            EarthRetargetBindPose bindPose =
                AssetDatabase.LoadAssetAtPath<EarthRetargetBindPose>(RetargetBindPosePath);
            if (bindPose == null)
            {
                bindPose = ScriptableObject.CreateInstance<EarthRetargetBindPose>();
                bindPose.name = "Earth Retarget Bind Pose";
                AssetDatabase.CreateAsset(bindPose, RetargetBindPosePath);
            }
            bindPose.Configure(
                EarthRetargetBindPose.ComputeAvatarHash(animator.avatar),
                EarthRetargetBindPose.ComputeSkeletonHash(entries),
                Vector3.forward,
                Vector3.up,
                entries);
            EditorUtility.SetDirty(bindPose);
            return bindPose;
        }

        // The JLPM skeleton omits FBX helper nodes and replaces the hips parent
        // with a world-up simulation bone. Rest and animated rotations MUST use
        // this same parent basis; a raw FBX localRotation adds the rig's -90deg
        // conversion root a second time when mapped back onto the visible rig.
        public static List<EarthRetargetBindBone> CaptureRetargetBindBones(Animator animator)
        {
            var entries = new List<EarthRetargetBindBone>(Bones.Length);
            for (int index = 0; index < Bones.Length; index++)
            {
                Transform bone = animator.GetBoneTransform(Bones[index]);
                if (bone == null) continue;
                entries.Add(new EarthRetargetBindBone(
                    Bones[index],
                    bone.name,
                    GetCollapsedLocalRotation(animator, Bones[index])));
            }
            return entries;
        }

        public static Quaternion GetCollapsedLocalRotation(Animator animator, HumanBodyBones humanoidBone)
        {
            Transform bone = animator.GetBoneTransform(humanoidBone);
            if (bone == null) return Quaternion.identity;
            Transform parent = bone.parent;
            while (parent != null && parent != animator.transform)
            {
                for (int index = 0; index < Bones.Length; index++)
                    if (animator.GetBoneTransform(Bones[index]) == parent)
                        return Normalize(Quaternion.Inverse(parent.rotation) * bone.rotation);
                parent = parent.parent;
            }
            return Normalize(Quaternion.Inverse(GetSimulationRotation(animator)) * bone.rotation);
        }

        private static Quaternion GetSimulationRotation(Animator animator) => Quaternion.LookRotation(
            Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized, Vector3.up);

        private static bool ContainsRequiredRetargetBones(
            IReadOnlyList<EarthRetargetBindBone> entries)
        {
            HumanBodyBones[] required =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot
            };
            for (int requiredIndex = 0; requiredIndex < required.Length; requiredIndex++)
            {
                bool found = false;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    if (entries[entryIndex].Bone != required[requiredIndex]) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y +
                value.z * value.z + value.w * value.w);
            if (magnitude < 0.0001f) return Quaternion.identity;
            float inverse = 1f / magnitude;
            return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
        }

        public static IReadOnlyList<string> Validate(MotionLibraryAsset library)
        {
            var errors = new List<string>();
            if (library == null) errors.Add("Library is null.");
            else
            {
                if (library.sourceRig == null) errors.Add("Source rig is missing.");
                if (library.clips == null || library.clips.Count == 0) errors.Add("No clips assigned.");
                else
                {
                    var stableIds = new HashSet<string>(StringComparer.Ordinal);
                    int searchableCount = 0;
                    for (int i = 0; i < library.clips.Count; i++)
                    {
                        MotionClipRecipe recipe = library.clips[i];
                        if (recipe == null || recipe.clip == null) errors.Add($"Clip recipe {i} is empty.");
                        else if (!recipe.clip.isHumanMotion) errors.Add($"{recipe.clip.name} is not imported as Humanoid motion.");
                        if (recipe != null && IsSearchableBaseMotion(recipe.role))
                        {
                            searchableCount++;
                            if (recipe.role == MotionClipRole.Locomotion &&
                                ResolveQueryTag(recipe.role, recipe.semantic) ==
                                PlanetEAMMCharacterController.UnsearchableQueryTag)
                                errors.Add($"{recipe.clip?.name ?? $"recipe {i}"}: locomotion semantic " +
                                           $"{recipe.semantic} has no directional EAMM query tag.");
                        }
                        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.stableId) &&
                            !stableIds.Add(recipe.stableId))
                            errors.Add($"Duplicate motion stable ID: {recipe.stableId}.");
                        if (recipe != null && recipe.contactStart > recipe.contactEnd)
                            errors.Add($"{recipe.clip?.name ?? $"recipe {i}"}: contact window is reversed.");
                    }
                    if (searchableCount == 0)
                        errors.Add("The catalog contains no searchable idle/start/locomotion/stop/pivot motion.");
                }
            }
            return errors;
        }

        private static void ValidateOrThrow(MotionLibraryAsset library)
        {
            IReadOnlyList<string> errors = Validate(library);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        public static string ResolveQueryTag(MotionClipRole role, MotionSemantic semantic)
        {
            if (role == MotionClipRole.Idle)
                return PlanetEAMMCharacterController.IdleQueryTag;
            if (role == MotionClipRole.Pivot)
                return PlanetEAMMCharacterController.PivotQueryTag;
            if (role == MotionClipRole.Start)
                return PlanetEAMMCharacterController.StartQueryTag;
            if (role == MotionClipRole.Stop)
                return PlanetEAMMCharacterController.StopQueryTag;
            if (role != MotionClipRole.Locomotion)
                return PlanetEAMMCharacterController.UnsearchableQueryTag;

            return semantic switch
            {
                MotionSemantic.WalkForward or MotionSemantic.RunForward =>
                    PlanetEAMMCharacterController.ForwardQueryTag,
                MotionSemantic.WalkBackward or MotionSemantic.RunBackward =>
                    PlanetEAMMCharacterController.BackwardQueryTag,
                MotionSemantic.RunLeft => PlanetEAMMCharacterController.LeftQueryTag,
                MotionSemantic.RunRight => PlanetEAMMCharacterController.RightQueryTag,
                _ => PlanetEAMMCharacterController.UnsearchableQueryTag
            };
        }

        private static bool IsSearchableBaseMotion(MotionClipRole role) => role is
            MotionClipRole.Idle or MotionClipRole.Start or MotionClipRole.Locomotion or
            MotionClipRole.Stop or MotionClipRole.Pivot;

        private static List<Transform> BuildTransformList(
            Animator animator,
            MotionMatchingData data,
            out Skeleton skeleton)
        {
            skeleton = new Skeleton();
            skeleton.AddJoint(new Skeleton.Joint("SimulationBone", 0, 0, Vector3.zero));
            var transforms = new List<Transform> { animator.transform };
            var indices = new Dictionary<Transform, int>();
            data.SkeletonToMecanim.Clear();
            for (int i = 0; i < Bones.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(Bones[i]);
                if (bone == null) continue;
                int parentIndex = 0;
                Transform parent = bone.parent;
                while (parent != null && !indices.TryGetValue(parent, out parentIndex)) parent = parent.parent;
                int index = skeleton.Joints.Count;
                indices[bone] = index;
                transforms.Add(bone);
                Vector3 parentPosition = parentIndex == 0
                    ? Vector3.ProjectOnPlane(animator.GetBoneTransform(HumanBodyBones.Hips).position, Vector3.up)
                    : transforms[parentIndex].position;
                Quaternion parentRotation = parentIndex == 0
                    ? GetSimulationRotation(animator)
                    : transforms[parentIndex].rotation;
                Vector3 collapsedOffset = Quaternion.Inverse(parentRotation) * (bone.position - parentPosition);
                skeleton.AddJoint(new Skeleton.Joint(bone.name, index, parentIndex, collapsedOffset, Bones[i]));
                data.SkeletonToMecanim.Add(new MotionMatchingData.JointToMecanim(bone.name, Bones[i]));
            }
            return transforms;
        }

        private static PoseVector[] SampleClip(
            GameObject instance,
            Animator animator,
            List<Transform> transforms,
            MotionClipRecipe recipe,
            float frameTime)
        {
            AnimationClip clip = recipe.clip;
            int frameCount = Mathf.Max(2, Mathf.CeilToInt(clip.length / frameTime) + 1);
            var positions = new float3[frameCount][];
            var rotations = new quaternion[frameCount][];
            Transform leftContactBone = animator.GetBoneTransform(HumanBodyBones.LeftToes) ??
                                        animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightContactBone = animator.GetBoneTransform(HumanBodyBones.RightToes) ??
                                         animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftContactPositions = new float3[frameCount];
            var rightContactPositions = new float3[frameCount];
            Vector3 syntheticOrigin = Vector3.zero;
            bool hasSyntheticOrigin = false;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float time = Mathf.Min(clip.length, frame * frameTime);
                clip.SampleAnimation(instance, time);
                positions[frame] = new float3[transforms.Count];
                rotations[frame] = new quaternion[transforms.Count];
                leftContactPositions[frame] = leftContactBone != null
                    ? (float3)leftContactBone.position
                    : new float3(float.PositiveInfinity);
                rightContactPositions[frame] = rightContactBone != null
                    ? (float3)rightContactBone.position
                    : new float3(float.PositiveInfinity);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 sampledSimulationPosition = Vector3.ProjectOnPlane(hips.position, Vector3.up);
                // Hips.forward is an imported bone axis, not the character's
                // travel facing. Sampling it injected pelvic sway into search
                // trajectories and a different basis into every hips pose.
                Quaternion sampledSimulationRotation = GetSimulationRotation(animator);
                Vector3 planarForward = sampledSimulationRotation * Vector3.forward;
                if (!hasSyntheticOrigin)
                {
                    syntheticOrigin = sampledSimulationPosition;
                    hasSyntheticOrigin = true;
                }
                Vector3 travelDirection = Quaternion.AngleAxis(recipe.nominalDirection, Vector3.up) *
                                          planarForward.normalized;
                Vector3 simulationPosition = syntheticOrigin +
                                             travelDirection * recipe.nominalSpeed * time;
                Quaternion simulationRotation = Quaternion.AngleAxis(
                    recipe.nominalYaw * time,
                    Vector3.up) * sampledSimulationRotation;
                positions[frame][0] = simulationPosition;
                rotations[frame][0] = simulationRotation;
                for (int joint = 1; joint < transforms.Count; joint++)
                {
                    Transform bone = transforms[joint];
                    int parentIndex = FindIncludedParentIndex(bone.parent, transforms);
                    // Keep the sampled body pose relative to its sampled hips frame,
                    // while the synthetic simulation bone carries authored nominal
                    // locomotion. This gives in-place clips useful trajectory data
                    // without baking translation into the visible skeleton twice.
                    Vector3 parentPosition = parentIndex == 0
                        ? sampledSimulationPosition
                        : transforms[parentIndex].position;
                    Quaternion parentRotation = parentIndex == 0
                        ? sampledSimulationRotation
                        : transforms[parentIndex].rotation;
                    positions[frame][joint] = Quaternion.Inverse(parentRotation) * (bone.position - parentPosition);
                    rotations[frame][joint] = Quaternion.Inverse(parentRotation) * bone.rotation;
                }
            }

            var poses = new PoseVector[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                int previous = Mathf.Max(0, frame - 1);
                var velocities = new float3[transforms.Count];
                var angularVelocities = new float3[transforms.Count];
                for (int joint = 0; joint < transforms.Count; joint++)
                    velocities[joint] = (positions[frame][joint] - positions[previous][joint]) / frameTime;
                bool leftContact = DetectFootContact(
                    leftContactPositions, frame, frameTime, recipe.loop);
                bool rightContact = DetectFootContact(
                    rightContactPositions, frame, frameTime, recipe.loop);
                poses[frame] = new PoseVector(
                    positions[frame],
                    rotations[frame],
                    velocities,
                    angularVelocities,
                    leftContact,
                    rightContact);
            }
            return poses;
        }

        public static bool DetectFootContact(
            float3[] worldPositions,
            int frame,
            float frameTime,
            bool looping)
        {
            int count = worldPositions != null ? worldPositions.Length : 0;
            if (count < 2 || frame < 0 || frame >= count || frameTime <= 0f ||
                !math.all(math.isfinite(worldPositions[frame])))
                return false;

            float minimumHeight = float.MaxValue;
            for (int index = 0; index < count; index++)
                if (math.all(math.isfinite(worldPositions[index])))
                    minimumHeight = math.min(minimumHeight, worldPositions[index].y);
            if (!math.isfinite(minimumHeight)) return false;

            int previous = frame > 0
                ? frame - 1
                : looping && count > 2 ? count - 2 : frame;
            int next = frame + 1 < count
                ? frame + 1
                : looping && count > 2 ? 1 : frame;
            bool oneSidedDifference = previous == frame || next == frame;
            float sampleSpan = (oneSidedDifference ? 1f : 2f) * frameTime;
            float verticalVelocity =
                (worldPositions[next].y - worldPositions[previous].y) / sampleSpan;
            float currentHeight = worldPositions[frame].y;
            bool nearGround = currentHeight <= minimumHeight + 0.045f;
            // At a 30 Hz database rate a sharp run/strafe minimum may fall
            // between samples or across the loop seam. A sampled local valley
            // is still a physical plant even when its finite-difference speed
            // exceeds the conservative walk threshold. The height band keeps
            // neighbouring low frames in the contact window so final IK has
            // time to acquire instead of receiving a one-frame impulse.
            bool localValley = currentHeight <= worldPositions[previous].y + 0.002f &&
                               currentHeight <= worldPositions[next].y + 0.002f;
            return nearGround && (localValley || math.abs(verticalVelocity) <= 0.75f);
        }

        private static bool HasBilateralFootCycle(PoseVector[] poses, out string summary)
        {
            int leftPlant = 0;
            int rightPlant = 0;
            int leftSwing = 0;
            int rightSwing = 0;
            for (int frame = 0; frame < poses.Length; frame++)
            {
                if (poses[frame].LeftFootContact) leftPlant++;
                else leftSwing++;
                if (poses[frame].RightFootContact) rightPlant++;
                else rightSwing++;
            }
            summary = $"left plant/swing={leftPlant}/{leftSwing}, " +
                      $"right plant/swing={rightPlant}/{rightSwing}";
            return leftPlant > 0 && rightPlant > 0 && leftSwing > 0 && rightSwing > 0;
        }

        private static int FindIncludedParentIndex(Transform parent, List<Transform> transforms)
        {
            while (parent != null)
            {
                int index = FindTransformIndex(parent, transforms);
                if (index >= 0) return index;
                parent = parent.parent;
            }
            return 0;
        }

        private static int FindTransformIndex(Transform target, List<Transform> transforms)
        {
            if (target == null) return -1;
            for (int i = 0; i < transforms.Count; i++) if (transforms[i] == target) return i;
            return -1;
        }

        private static void ConfigureFeatures(MotionMatchingData data)
        {
            data.AnimationDatas = new List<AnimationData>();
            data.TrajectoryFeatures = new List<MotionMatchingData.TrajectoryFeature>
            {
                new()
                {
                    Name = "FuturePosition",
                    FeatureType = MotionMatchingData.TrajectoryFeature.Type.Position,
                    FramesPrediction = new[] { 3, 6, 9, 12 },
                    SimulationBone = true,
                    ZeroY = true,
                    IsMainPositionFeature = true
                },
                new()
                {
                    Name = "FutureDirection",
                    FeatureType = MotionMatchingData.TrajectoryFeature.Type.Direction,
                    FramesPrediction = new[] { 3, 6, 9, 12 },
                    SimulationBone = true,
                    ZeroY = true
                }
            };
            data.PoseFeatures = new List<MotionMatchingData.PoseFeature>
            {
                Pose("LeftFootPosition", MotionMatchingData.PoseFeature.Type.Position, HumanBodyBones.LeftFoot),
                Pose("LeftFootVelocity", MotionMatchingData.PoseFeature.Type.Velocity, HumanBodyBones.LeftFoot),
                Pose("RightFootPosition", MotionMatchingData.PoseFeature.Type.Position, HumanBodyBones.RightFoot),
                Pose("RightFootVelocity", MotionMatchingData.PoseFeature.Type.Velocity, HumanBodyBones.RightFoot),
                Pose("HipsVelocity", MotionMatchingData.PoseFeature.Type.Velocity, HumanBodyBones.Hips)
            };
            EllipseFeatureExtractor ellipse = null;
            HeightFeatureExtractor height = null;
            UnityEngine.Object[] children = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(data));
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] is EllipseFeatureExtractor existing) ellipse = existing;
                if (children[i] is HeightFeatureExtractor existingHeight) height = existingHeight;
            }
            if (ellipse == null)
            {
                ellipse = ScriptableObject.CreateInstance<EllipseFeatureExtractor>();
                ellipse.name = "FutureEllipseExtractor";
                AssetDatabase.AddObjectToAsset(ellipse, data);
            }
            if (height == null)
            {
                height = ScriptableObject.CreateInstance<HeightFeatureExtractor>();
                height.name = "FutureHeightExtractor";
                AssetDatabase.AddObjectToAsset(height, data);
            }
            data.EnvironmentFeatures = new List<MotionMatchingData.TrajectoryFeature>
            {
                new()
                {
                    Name = "FutureEllipse",
                    FeatureType = MotionMatchingData.TrajectoryFeature.Type.Custom3D,
                    FramesPrediction = new[] { 3, 6, 12 },
                    FeatureExtractor = ellipse
                },
                new()
                {
                    Name = "FutureHeight",
                    FeatureType = MotionMatchingData.TrajectoryFeature.Type.Custom2D,
                    FramesPrediction = new[] { 3, 6, 12 },
                    FeatureExtractor = height
                }
            };
        }

        private static MotionMatchingData.PoseFeature Pose(
            string name,
            MotionMatchingData.PoseFeature.Type type,
            HumanBodyBones bone) => new() { Name = name, FeatureType = type, Bone = bone };

        private static void EnsureEnvironmentSearch(string libraryName)
        {
            string path = $"{OutputFolder}/{libraryName}_EnvironmentSearch.asset";
            EnvironmentMotionMatchingSearch search =
                AssetDatabase.LoadAssetAtPath<EnvironmentMotionMatchingSearch>(path);
            if (search != null) return;
            search = ScriptableObject.CreateInstance<EnvironmentMotionMatchingSearch>();
            search.EnvironmentAccelerationConsts = new EnvironmentAccelerationConsts(0.05f, 8, 1f);
            search.ObstacleDistanceThreshold = 1.35f;
            search.Anticipation = 2f;
            AssetDatabase.CreateAsset(search, path);
        }

        private static void MigrateLegacySearchAsset(string libraryName, string dataName)
        {
            if (string.Equals(libraryName, dataName, StringComparison.Ordinal)) return;
            string legacyPath = $"{OutputFolder}/{libraryName}_EnvironmentSearch.asset";
            string dataPath = $"{OutputFolder}/{dataName}_EnvironmentSearch.asset";
            if (AssetDatabase.LoadMainAssetAtPath(dataPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(legacyPath) == null) return;
            string error = AssetDatabase.MoveAsset(legacyPath, dataPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not migrate EAMM search asset: {error}");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
