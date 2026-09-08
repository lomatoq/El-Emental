using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Authoring.Editor.MotionMatching;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    public static class LocomotionRhythmAuthoring
    {
        public const string CatalogPath = "Assets/Elemental/Content/Profiles/LocomotionClipCatalog.asset";
        [MenuItem("Elemental/Character/Repair Locomotion Capsule Materials")]
        public static void RepairLocomotionCapsuleMaterials()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                "Assets/Elemental/Content/Physics/CharacterFrictionless.physicMaterial");
            if (material == null) throw new InvalidOperationException("Existing CharacterFrictionless material is missing.");
            int count = 0;
            foreach (var motor in Object.FindObjectsByType<Elemental.Runtime.Characters.PlanetMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!motor.gameObject.scene.IsValid()) continue;
                var capsule = motor.GetComponent<CapsuleCollider>();
                if (capsule == null || capsule.sharedMaterial == material) continue;
                Undo.RecordObject(capsule, "Restore motor capsule material");
                capsule.sharedMaterial = material;
                EditorUtility.SetDirty(capsule);
                EditorSceneManager.MarkSceneDirty(motor.gameObject.scene);
                count++;
            }
            Debug.Log($"[LocomotionRhythm] Restored existing frictionless material on {count} motor capsules; save the scene.");
        }

        [MenuItem("Elemental/Character/Bake EAMM From Current User Locomotion")]
        public static void BakeCurrentUserLocomotion()
        {
            Directory.CreateDirectory("BuildReports/LocomotionRhythm");
            try
            {
                BakeCurrentUserLocomotionCore();
                File.WriteAllText("BuildReports/LocomotionRhythm/bake-status.txt", "PASS: derived pose binary read back; every current user query tag registered. " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception exception)
            {
                File.WriteAllText("BuildReports/LocomotionRhythm/bake-status.txt", exception.ToString());
                throw;
            }
        }
        private static void BakeCurrentUserLocomotionCore()
        {
            Install();
            var catalog = AssetDatabase.LoadAssetAtPath<LocomotionClipCatalog>(CatalogPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            BlendTree locomotion = null;
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.name == "Locomotion") locomotion = child.state.motion as BlendTree;
            if (locomotion == null || locomotion.blendParameter != "MoveX" || locomotion.blendParameterY != "MoveY")
                throw new InvalidOperationException("Expected the existing MoveX/MoveY locomotion tree; source controller was not changed.");
            var recipes = new List<MotionClipRecipe>();
            foreach (var child in locomotion.children)
            {
                if (child.motion is not AnimationClip clip)
                    throw new InvalidOperationException("A nested locomotion tree requires explicit directional metadata before baking.");
                var metadata = catalog.Find(clip);
                if (metadata == null) throw new InvalidOperationException("Clip measurement missing: " + clip.name);
                Vector2 direction = child.position;
                bool idle = direction.sqrMagnitude < .0001f;
                MotionSemantic semantic = idle ? MotionSemantic.NeutralIdle :
                    Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                        ? (direction.x < 0 ? MotionSemantic.RunLeft : MotionSemantic.RunRight)
                        : direction.y < 0 ? (metadata.NominalSpeed > 2f ? MotionSemantic.RunBackward : MotionSemantic.WalkBackward)
                        : metadata.NominalSpeed > 3f ? MotionSemantic.RunForward : MotionSemantic.WalkForward;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long fileId);
                string stableId = $"user.locomotion.{guid}.{fileId}";
                metadata.BlendPosition = direction; metadata.SourceQueryTag = stableId;
                recipes.Add(new MotionClipRecipe { stableId = stableId, clip = clip,
                    role = idle ? MotionClipRole.Idle : MotionClipRole.Locomotion, semantic = semantic,
                    nominalSpeed = idle ? 0f : metadata.NominalSpeed,
                    nominalDirection = idle ? 0f : Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg,
                    loop = true });
            }
            var library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            if (library == null) throw new InvalidOperationException("Production EAMM library missing.");
            // Replace only searchable idle/locomotion recipes. Every action,
            // recovery, pivot and its source reference remains authored as before.
            library.clips.RemoveAll(r => r.role is MotionClipRole.Idle or MotionClipRole.Locomotion);
            library.clips.InsertRange(0, recipes);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            var data = MotionLibraryBuilder.Bake(library);
            if (!new global::MotionMatching.PoseSerializer().Deserialize(data.GetAssetPath(), data.name, data, out var baked))
                throw new InvalidOperationException("Derived pose binary could not be read after bake.");
            try
            {
                foreach (var recipe in recipes)
                {
                    var tag = baked.GetTag(recipe.stableId);
                    int valid = 0;
                    for (int r = 0; r < tag.NumberRanges; r++)
                        for (int f = tag.GetStartRanges()[r]; f < tag.GetEndRanges()[r]; f++)
                            if (baked.IsPoseValidForPrediction(f)) valid++;
                    int fullCycle = Mathf.Max(2, Mathf.FloorToInt(recipe.clip.length / baked.FrameTime));
                    if (valid < fullCycle)
                        throw new InvalidOperationException($"{recipe.stableId} exposes {valid} valid prediction frames, fewer than one full authored cycle ({fullCycle}).");
                }
            }
            finally { baked.Dispose(); }
            Debug.Log($"[LocomotionRhythm] Rebuilt derived EAMM database with exactly {recipes.Count} current user locomotion clips. Controller assignments and non-locomotion recipes unchanged.");
        }
        [MenuItem("Elemental/Character/Install Locomotion Rhythm Metadata")]
        public static void Install()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EarthHumanoidMotionSetup.ControllerPath);
            if (controller == null) throw new InvalidOperationException("Current character controller missing.");
            var clips = new HashSet<AnimationClip>();
            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state.name == "Locomotion") Collect(child.state.motion, clips);
            var catalog = AssetDatabase.LoadAssetAtPath<LocomotionClipCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LocomotionClipCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var entries = new List<LocomotionClipCatalog.Entry>();
            var report = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EarthHumanoidMotionSetup.CanonicalCharacterPath);
            if (prefab == null) throw new InvalidOperationException("Canonical Humanoid needed to measure stride.");
            GameObject sampleRig = Object.Instantiate(prefab);
            sampleRig.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                catalog.ReferenceLegLengthMeters = LocomotionClipCatalog.MeasureLegLength(sampleRig.GetComponentInChildren<Animator>());
                report.Add($"Reference world leg length={catalog.ReferenceLegLengthMeters:F5}m; runtime speed/stride use visible world leg length divided by this reference.");
                foreach (AnimationClip clip in clips)
                {
                    var previousMetadata = catalog.Find(clip);
                    float speed = clip.averageSpeed.magnitude;
                    string measurement = "source root average speed";
                    if (speed < .12f && clip.length > .1f)
                    {
                        Animator animator = sampleRig.GetComponentInChildren<Animator>();
                        Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
                        for (int sample = 0; sample < 61; sample++)
                        {
                            clip.SampleAnimation(sampleRig, clip.length * sample / 60f);
                            Vector3 p = sampleRig.transform.InverseTransformVector(foot.position - hips.position);
                            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                        }
                        Vector3 span = max - min;
                        speed = 2f * Mathf.Max(span.x, span.z) / clip.length;
                        measurement = "measured in-place foot excursion / cycle";
                    }
                    if (clip.length <= .1f || clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0) speed = 0f;
                    entries.Add(new LocomotionClipCatalog.Entry { Clip = clip, NominalSpeed = speed,
                        CycleSeconds = clip.length, LeftContact = Curve(clip, EarthAnimationClipMetadata.LeftFootContact),
                        RightContact = Curve(clip, EarthAnimationClipMetadata.RightFootContact), Measurement = measurement,
                        BlendPosition = previousMetadata != null ? previousMetadata.BlendPosition : Vector2.zero,
                        SourceQueryTag = previousMetadata != null ? previousMetadata.SourceQueryTag : null });
                    report.Add($"{AssetDatabase.GetAssetPath(clip)} | {clip.name}: {speed:F3}m/s, {clip.length:F3}s ({measurement})");
                }
            }
            finally { Object.DestroyImmediate(sampleRig); }
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog);
            foreach (HumanoidCharacterPresentation presentation in Object.FindObjectsByType<HumanoidCharacterPresentation>(FindObjectsInactive.Include))
            {
                if (!presentation.gameObject.scene.IsValid()) continue;
                var rhythm = presentation.GetComponent<LocomotionRhythmController>();
                if (rhythm == null) rhythm = Undo.AddComponent<LocomotionRhythmController>(presentation.gameObject);
                rhythm.Configure(catalog); EditorUtility.SetDirty(rhythm);
                EditorSceneManager.MarkSceneDirty(presentation.gameObject.scene);
            }
            Directory.CreateDirectory("BuildReports/LocomotionRhythm");
            File.WriteAllLines("BuildReports/LocomotionRhythm/metadata.txt", report);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LocomotionRhythm] Catalog measured {entries.Count} existing user clips; controller and clips unchanged. Save scene to persist adapters.");
        }
        private static AnimationCurve Curve(AnimationClip clip, string name)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.propertyName == name) return AnimationUtility.GetEditorCurve(clip, binding);
            return null;
        }
        private static void Collect(Motion motion, HashSet<AnimationClip> clips)
        {
            if (motion is AnimationClip clip) clips.Add(clip);
            else if (motion is BlendTree tree) foreach (var child in tree.children) Collect(child.motion, clips);
        }
    }
}
