using System;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Fire;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishFireStreamInstaller
    {
        public const string ProfilePath="Assets/Elemental/Content/VFX/Fire/Fire_Stream_Local.asset";
        [MenuItem("Elemental/QA/Hard Polish/G05 Bind Local Fire Stream Only")]
        public static void InstallActiveScene()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity")
                throw new InvalidOperationException("Open the existing EarthCoreSlice scene; this installer never rebuilds or replaces a scene.");
            Install(scene);Undo.FlushUndoRecordObjects();EditorSceneManager.SaveScene(scene);
        }
        public static FireStreamPresentationBinding Install(Scene scene)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new ArgumentException("A loaded production scene is required.");
            T Single<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
            var flow=Single<FrontendFlowController>();var duel=flow.MatchController;var gravity=Single<GravityWorldBehaviour>();
            var camera=HardPolishSceneIntegration.ResolveOutputCamera(scene);
            if(duel.PlayerTransform==null||duel.BotTransform==null)throw new InvalidOperationException("Bind both duel actor roots first.");
            var player=duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var bot=duel.BotTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var existing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FireStreamPresentationBinding>(true)).ToArray();
            if(existing.Length>1)throw new InvalidOperationException("Multiple local Fire owners require explicit cleanup.");
            if(existing.Length==1) { BindInput(duel,existing[0]); return existing[0]; } // Preserve the existing profile and repair the explicit input reference.
            FireVisualProfile profile=BuildProfile();
            var owner=new GameObject("Local Fire Stream Integration");SceneManager.MoveGameObjectToScene(owner,scene);
            Undo.RegisterCreatedObjectUndo(owner,"Bind local Fire stream");
            var world=owner.AddComponent<FireWorldBehaviour>();
            var serialized=new SerializedObject(world);
            serialized.FindProperty("gravityWorld").objectReferenceValue=gravity;
            serialized.FindProperty("collisionMask").intValue=~0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var lights=owner.AddComponent<FireLightPool>();
            var localSession=player.gameObject.GetComponent<FireStreamSession>()??Undo.AddComponent<FireStreamSession>(player.gameObject);
            var rivalSession=bot.gameObject.GetComponent<FireStreamSession>()??Undo.AddComponent<FireStreamSession>(bot.gameObject);
            var binding=owner.AddComponent<FireStreamPresentationBinding>();
            binding.Configure(world,duel,flow,player,bot,localSession,rivalSession,profile,camera,lights);
            BindInput(duel,binding);
            EditorUtility.SetDirty(binding);EditorUtility.SetDirty(world);EditorSceneManager.MarkSceneDirty(scene);
            return binding;
        }
        private static void BindInput(EarthMvpDuelController duel,FireStreamPresentationBinding binding)
        {
            var input=duel.PlayerTransform.GetComponentsInChildren<Elemental.Input.Gestures.MagicInputController>(true).Single();
            if(input.GetComponent<Elemental.Input.Actions.EarthInputAdapter>()==null)
                throw new InvalidOperationException("The player's existing semantic input adapter must already be configured.");
            Undo.RecordObject(input,"Bind Fire stream input");
            input.ConfigureFireStream(binding.PlayerSession);EditorUtility.SetDirty(input);
            var scar=duel.gameObject.scene.GetRootGameObjects()
                .SelectMany(r=>r.GetComponentsInChildren<Elemental.Presentation.VFX.EarthSurfaceScarPool>(true))
                .Single(s=>s.ConfiguredExecutor==input.EarthExecutor||s.ConfiguredExecutor==null&&s.gameObject.name=="Earth Magic Feedback");
            if(scar.ConfiguredExecutor==null)
            {
                var profile=AssetDatabase.LoadAssetAtPath<Elemental.Presentation.VFX.EarthFeedbackProfile>("Assets/Elemental/Content/Profiles/EarthFeedbackProfile.asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/EarthSurfaceScarDecal.mat");
                var center=duel.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PointPlanetGravitySource>(true)).Single().transform;
                if(profile==null||material==null)throw new InvalidOperationException("Existing scar profile/material is missing.");
                Undo.RecordObject(scar,"Configure saved surface scar pool");
                scar.ConfigureFireOnly();scar.Configure(input.EarthExecutor,profile,material,center);EditorUtility.SetDirty(scar);
            }
            Undo.RecordObject(binding,"Bind shared Fire soot pool");
            binding.ConfigureAbilityScarPool(scar);EditorUtility.SetDirty(binding);
            EditorSceneManager.MarkSceneDirty(duel.gameObject.scene);
        }
        private static FireVisualProfile BuildProfile()
        {
            var existing=AssetDatabase.LoadAssetAtPath<FireVisualProfile>(ProfilePath);
            if(existing!=null)
            {
                if(existing.Backend!=FireVisualBackendSelection.CpuMesh||!existing.CoherentBody||!existing.IsValid)
                    throw new InvalidOperationException("The existing local stream profile must explicitly support CPU coherent rendering: "+ProfilePath);
                return existing;
            }
            var source=AssetDatabase.LoadAssetAtPath<FireVisualProfile>("Assets/Elemental/Content/VFX/Fire/Fire_Default.asset");
            if(source==null)throw new InvalidOperationException("Import the existing Fire_Default visual profile before local stream binding.");
            var profile=UnityEngine.Object.Instantiate(source);profile.name="Fire_Stream_Local";
            profile.Backend=FireVisualBackendSelection.CpuMesh;profile.CoherentBody=true;
            profile.CpuCapacity=512;profile.SpawnRate=110;profile.FlameMinWidth=.22f;profile.FlameMaxWidth=.38f;
            profile.FlameMinAspect=1.4f;profile.FlameMaxAspect=2.1f;
            profile.MinLifetime=.45f;profile.MaxLifetime=.7f;profile.LightIntensity=1.4f;profile.LightRange=3.5f;
            if(profile.CpuMaterial==null||profile.CoherentBodyShader==null||!profile.IsValid)
            {UnityEngine.Object.DestroyImmediate(profile);throw new InvalidOperationException("Existing CPU/coherent-body shader references are missing.");}
            AssetDatabase.CreateAsset(profile,ProfilePath);AssetDatabase.SaveAssets();return profile;
        }
    }
}
