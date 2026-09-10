using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.DistantScenery;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishSceneIntegration
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string FogPath = "Assets/Elemental/Content/Profiles/ValleyAtmosphereHardPolish.asset";
        [Serializable] private sealed class MeshEntry { public string path, guid; public int triangles; }
        [Serializable] private sealed class Report
        {
            public string utc, scene, fogProfile;
            public int authoredBefore, authoredAfter;
            public float maximumAuthoredPositionChange, maximumAuthoredRotationChange, maximumAuthoredScaleChange;
            public bool sceneSaved; public string error; public string[] newMeshBoundsConflicts;
            public List<MeshEntry> floatingMeshes = new();
        }
        private readonly struct AuthoredPose
        {
            public readonly Vector3 Position,Scale; public readonly Quaternion Rotation;
            public AuthoredPose(Transform source){Position=source.position;Rotation=source.rotation;Scale=source.localScale;}
        }
        private static Dictionary<string,AuthoredPose> CaptureAuthored(DistantBackdrop backdrop)
        {
            var result=new Dictionary<string,AuthoredPose>();
            foreach(Transform landmark in backdrop.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("View_")))
            foreach(Transform node in landmark.GetComponentsInChildren<Transform>(true))
            {
                string path=node.name;Transform parent=node.parent;
                while(parent!=null&&parent!=landmark.parent){path=parent.name+"/"+path;parent=parent.parent;}
                result.Add(path,new AuthoredPose(node));
            }
            return result;
        }
        private sealed class MeshSwap { public Mesh asset, candidate, backup; }

        internal static Camera ResolveOutputCamera(Scene scene)
        {
            var cinematic = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CinematicMenuCamera>(true)).Single();
            var camera = new SerializedObject(cinematic).FindProperty("outputCamera").objectReferenceValue as Camera;
            if (camera == null || camera.gameObject.scene != scene)
                throw new InvalidOperationException("The existing cinematic owner must reference the scene output camera.");
            return camera;
        }

        public static void InstallPresentation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install outside Play Mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallBindings(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        private static void InstallBindings(Scene scene)
        {
            T Single<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                HardPolishFireStreamInstaller.Install(scene);
                GoldRespawnSetup.Install();
                HardPolishMatchStageInstaller.Install(scene);
                HardPolishResponseInstaller.Install();
                var camera = ResolveOutputCamera(scene);
                var sky = Single<CelestialSystemBehaviour>();
                foreach (var clarity in camera.GetComponents<EarthChargeCameraLookdevV2>())
                { clarity.BindCelestialReadability(sky); EditorUtility.SetDirty(clarity); }

                var atmosphere = Single<ValleyAtmosphereController>();
                var serializedAtmosphere = new SerializedObject(atmosphere);
                var sourceFog = (ValleyAtmosphereProfile)serializedAtmosphere.FindProperty("profile").objectReferenceValue;
                var fog = AssetDatabase.LoadAssetAtPath<ValleyAtmosphereProfile>(FogPath);
                if (fog == null)
                {
                    fog = UnityEngine.Object.Instantiate(sourceFog); fog.name = "ValleyAtmosphereHardPolish";
                    fog.MidAerialOpacityMultiplier = .88f; AssetDatabase.CreateAsset(fog, FogPath);
                }
                serializedAtmosphere.FindProperty("profile").objectReferenceValue = fog;
                serializedAtmosphere.ApplyModifiedPropertiesWithoutUndo();
                atmosphere.ConfigureStorm(Single<FrontendFlowController>(), AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Elemental/Content/Audio/DistantStormRumble.wav"));
                EditorUtility.SetDirty(atmosphere);
                foreach (var dust in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EarthSurfaceWindDust>(true)))
                { dust.ConfigureViewCamera(camera); EditorUtility.SetDirty(dust); }
                foreach (var flock in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DistantBirdFlock>(true)))
                {
                    flock.ConfigureViewCamera(camera);
                    var serializedFlock = new SerializedObject(flock); serializedFlock.FindProperty("birdCount").intValue = 40;
                    serializedFlock.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(flock);
                }
        }

        [MenuItem("Elemental/QA/Hard Polish/Install Validated Integrations In Existing Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install outside Play Mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            T Single<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
            var backdrop = Single<DistantBackdrop>();
            var before = CaptureAuthored(backdrop);
            var geometryBefore = backdrop.CaptureExistingGeometry();
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scene = ScenePath, authoredBefore = before.Count };
            var swaps = new List<MeshSwap>();
            bool published = false;
            try
            {
                // User direction: retain original authored island assets. Supplements
                // have their own mesh identities; never overwrite existing geography.
                for (int index = 6; index < 12; index++)
                for (int lod = 0; lod < 2; lod++)
                {
                    string path = $"Assets/Elemental/Content/Environment/DistantStone/Meshes/Island_{index}_LOD{lod}.asset";
                    Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (asset == null) throw new InvalidOperationException("Existing floating mesh is missing: " + path);
                    report.floatingMeshes.Add(new MeshEntry { path = path, guid = AssetDatabase.AssetPathToGUID(path), triangles = asset.triangles.Length / 3 });
                }
                report.newMeshBoundsConflicts = backdrop.InspectNewMeshConflicts(geometryBefore);
                backdrop.RefreshExistingGeometryAndAddSupplements(geometryBefore);
                EditorUtility.SetDirty(backdrop);
                var after = CaptureAuthored(backdrop);
                report.authoredAfter = after.Count;
                foreach (var item in before)
                {
                    if (!after.TryGetValue(item.Key, out AuthoredPose pose)) throw new InvalidOperationException("Authored landmark was rejected: " + item.Key);
                    report.maximumAuthoredPositionChange = Mathf.Max(report.maximumAuthoredPositionChange, Vector3.Distance(pose.Position, item.Value.Position));
                    report.maximumAuthoredRotationChange=Mathf.Max(report.maximumAuthoredRotationChange,Quaternion.Angle(pose.Rotation,item.Value.Rotation));
                    report.maximumAuthoredScaleChange=Mathf.Max(report.maximumAuthoredScaleChange,Vector3.Distance(pose.Scale,item.Value.Scale));
                }
                if (report.maximumAuthoredRotationChange>.01f || report.maximumAuthoredScaleChange>.0001f)
                    throw new InvalidOperationException("Rebuild changed saved authored rotation, scale, or LOD hierarchy; scene publication rejected.");
                if (report.maximumAuthoredPositionChange > .01f)
                    throw new InvalidOperationException("New floating bounds moved authored geography by " + report.maximumAuthoredPositionChange + "m. Review placement before publishing.");

                InstallBindings(scene);
                report.fogProfile = FogPath;
                AssetDatabase.SaveAssets();
                foreach (MeshEntry entry in report.floatingMeshes)
                    if (AssetDatabase.AssetPathToGUID(entry.path) != entry.guid) throw new InvalidOperationException("Floating mesh GUID changed: " + entry.path);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.sceneSaved = true;
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                if (published)
                {
                    foreach (MeshSwap swap in swaps) { EditorUtility.CopySerialized(swap.backup, swap.asset); EditorUtility.SetDirty(swap.asset); }
                    AssetDatabase.SaveAssets();
                }
                throw;
            }
            finally
            {
                foreach (MeshSwap swap in swaps) { UnityEngine.Object.DestroyImmediate(swap.candidate); UnityEngine.Object.DestroyImmediate(swap.backup); }
                Directory.CreateDirectory("BuildReports/HardPolish/Integration");
                File.WriteAllText("BuildReports/HardPolish/Integration/SceneInstall.json", JsonUtility.ToJson(report, true));
            }
        }
    }
}
