using System.Collections.Generic;
using Elemental.Authoring;
using Elemental.Presentation.Camera;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using MotionMatching;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor.MotionMatching
{
    public static class EAMMRuntimeAudit
    {
        [MenuItem("Elemental/Diagnostics/Audit EAMM Runtime")]
        public static void Audit()
        {
            MotionMatchingController[] controllers = Object.FindObjectsByType<MotionMatchingController>(
                FindObjectsInactive.Include);
            EAMMBasePoseBridge[] bridges = Object.FindObjectsByType<EAMMBasePoseBridge>(
                FindObjectsInactive.Include);
            int ready = 0;
            for (int i = 0; i < bridges.Length; i++)
                if (bridges[i].IsReady) ready++;

            var missing = new List<string>();
            GameObject[] objects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(objects[i]);
                if (count > 0) missing.Add(GetPath(objects[i].transform) + " (" + count + ")");
            }

            string missingSummary = missing.Count == 0 ? "none" : string.Join(", ", missing);
            Debug.Log(
                $"[EAMM Audit] controllers={controllers.Length}, bridges={bridges.Length}, " +
                $"ready={ready}, missingScripts={missingSummary}.");
            for (int i = 0; i < controllers.Length; i++)
            {
                MotionMatchingController controller = controllers[i];
                string clipSummary = ResolveClipSummary(controller);
                float targetSpeed = controller.CharacterController is PlanetEAMMCharacterController adapter
                    ? adapter.GetTargetSpeed()
                    : -1f;
                int allowedPoses = 0;
                if (controller.TagMask.IsCreated)
                    for (int poseIndex = 0; poseIndex < controller.TagMask.Length; poseIndex++)
                        if (controller.TagMask[poseIndex]) allowedPoses++;
                string queryState = controller.CharacterController is PlanetEAMMCharacterController planetAdapter
                    ? (planetAdapter.HasLocomotionQuery
                        ? planetAdapter.CurrentQueryTag
                        : "unset")
                    : "n/a";
                Debug.Log(
                    $"[EAMM Controller] path={GetPath(controller.transform)}, " +
                    $"active={controller.gameObject.activeInHierarchy}, enabled={controller.enabled}, " +
                    $"initialized={controller.RuntimeInitialized}, status={controller.InitializationStatus}, " +
                    $"database={controller.MMData != null}, search={controller.Search != null}, " +
                    $"adapter={controller.CharacterController != null}, " +
                    $"skeleton={(controller.SkeletonTransforms != null ? controller.SkeletonTransforms.Length : 0)}, " +
                    $"frame={controller.CurrentFrame}/{controller.ContinuousFrame:F2}, " +
                    $"lastSearch={controller.LastMMSearchFrame}, searchIn={controller.SearchTimeLeft:F3}, " +
                    $"poses={(controller.PoseSet != null ? controller.PoseSet.NumberPoses : 0)}, " +
                    $"clips={(controller.PoseSet != null ? controller.PoseSet.NumberClips : 0)}, " +
                    $"tags={(controller.PoseSet != null ? controller.PoseSet.NumberTags : 0)}, " +
                    $"allowed={allowedPoses}, query={queryState}, " +
                    $"features={(controller.FeatureSet != null ? controller.FeatureSet.NumberFeatureVectors : 0)}, " +
                    $"targetSpeed={targetSpeed:F3}, velocity={controller.Velocity}, " +
                    $"angularVelocity={controller.AngularVelocity}, clip={clipSummary}.");
            }
            for (int i = 0; i < bridges.Length; i++)
            {
                EAMMBasePoseBridge bridge = bridges[i];
                Debug.Log(
                    $"[EAMM Bridge] path={GetPath(bridge.transform)}, active={bridge.gameObject.activeInHierarchy}, " +
                    $"enabled={bridge.enabled}, ready={bridge.IsReady}, status={bridge.InitializationStatus}.");
            }

            AuditActor("Planet Character");
            AuditActor("Rumble Linebreaker Bot");
            EarthCinemachineCameraController cameraController =
                Object.FindAnyObjectByType<EarthCinemachineCameraController>(
                    FindObjectsInactive.Include);
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            GameObject player = GameObject.Find("Planet Character");
            float targetDistance = camera != null && player != null
                ? Vector3.Distance(camera.transform.position, player.transform.position)
                : -1f;
            Debug.Log(
                $"[Camera Audit] live={cameraController != null && cameraController.IsLive}, " +
                $"mainCamera={camera != null}, targetDistance={targetDistance:F3}, " +
                $"worldUpFrame={cameraController?.WorldUpFrame != null}, " +
                $"aimPivot={cameraController?.AimPivot != null}.");
        }

        private static void AuditActor(string actorName)
        {
            GameObject actor = GameObject.Find(actorName);
            PlanetMotor motor = actor != null ? actor.GetComponent<PlanetMotor>() : null;
            Rigidbody body = actor != null ? actor.GetComponent<Rigidbody>() : null;
            GravityBody gravity = actor != null ? actor.GetComponent<GravityBody>() : null;
            Debug.Log(
                $"[Actor Audit] actor={actorName}, found={actor != null}, " +
                $"position={(actor != null ? actor.transform.position : Vector3.zero)}, " +
                $"grounded={motor != null && motor.IsGrounded}, " +
                $"move={(motor != null ? motor.LastCommand.Move : default)}, " +
                $"motorSpeed={(motor != null ? motor.Telemetry.Speed : 0f):F3}, " +
                $"desiredSpeed={(motor != null ? motor.Telemetry.DesiredSpeed : 0f):F3}, " +
                $"localUp={(motor != null ? motor.LocalUp : Vector3.zero)}, " +
                $"velocity={(body != null ? body.linearVelocity : Vector3.zero)}, " +
                $"useGravity={(body != null && body.useGravity)}, " +
                $"customGravity={(gravity != null ? gravity.LastAcceleration : Vector3.zero)}.");
        }

        private static string ResolveClipSummary(MotionMatchingController controller)
        {
            if (controller == null || controller.PoseSet == null) return "unavailable";
            int clipIndex = -1;
            PoseSet.AnimationClip active = default;
            for (int index = 0; index < controller.PoseSet.NumberClips; index++)
            {
                PoseSet.AnimationClip candidate = controller.PoseSet.GetAnimationClip(index);
                if (controller.CurrentFrame < candidate.Start || controller.CurrentFrame >= candidate.End) continue;
                clipIndex = index;
                active = candidate;
                break;
            }

            if (clipIndex < 0) return $"none@{controller.CurrentFrame}";
            MotionLibraryAsset library = AssetDatabase.LoadAssetAtPath<MotionLibraryAsset>(
                "Assets/Elemental/Content/Characters/MotionMatching/EarthMotionLibrary.asset");
            if (library == null) return $"index={clipIndex}[{active.Start},{active.End})";
            int searchableIndex = 0;
            for (int recipeIndex = 0; recipeIndex < library.clips.Count; recipeIndex++)
            {
                MotionClipRecipe recipe = library.clips[recipeIndex];
                if (recipe == null || !IsSearchableBaseMotion(recipe.role)) continue;
                if (searchableIndex++ != clipIndex) continue;
                return $"{recipe.stableId}/{recipe.semantic}/speed={recipe.nominalSpeed:F2}" +
                       $"[{active.Start},{active.End})";
            }
            return $"index={clipIndex}[{active.Start},{active.End})";
        }

        private static bool IsSearchableBaseMotion(MotionClipRole role) => role is
            MotionClipRole.Idle or MotionClipRole.Start or MotionClipRole.Locomotion or
            MotionClipRole.Stop or MotionClipRole.Pivot;

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
