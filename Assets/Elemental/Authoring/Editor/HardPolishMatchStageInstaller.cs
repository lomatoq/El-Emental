using System;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Elemental.Authoring.Editor
{
    public static class HardPolishMatchStageInstaller
    {
        [MenuItem("Elemental/QA/Hard Polish/G08 Bind Match Stage Only")]
        public static void InstallActiveScene()
        {
            Scene scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity")throw new InvalidOperationException("Open the existing production scene first.");
            Install(scene);EditorSceneManager.SaveScene(scene);
        }
        public static MatchPresentationStage Install(Scene scene)
        {
            EnsureStageRenderingLayer();
            T Single<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
            var existing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MatchPresentationStage>(true)).ToArray();
            if(existing.Length>1)throw new InvalidOperationException("Multiple stage owners require explicit cleanup.");
            if(existing.Length==1)return existing[0];
            var flow=Single<FrontendFlowController>();var duel=flow.MatchController;var hud=Single<EarthDuelHud>();
            var gate=Single<EarthSceneReadinessGate>();var cinematic=Single<CinematicMenuCamera>();
            var camera=HardPolishSceneIntegration.ResolveOutputCamera(scene);
            var player=duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var bot=duel.BotTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var meshes=new[]{"V5_Pebble_16","V5_Pebble_17","V5_Pebble_18","V5_Pebble_19"}.Select(n=>AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Elemental/Content/GraphicsV5/Rocks/"+n+".asset")).ToArray();
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
            if(meshes.Any(m=>m==null)||material==null)throw new InvalidOperationException("Bake the existing rock library first.");
            var owner=new GameObject("Match Cosmetic Stage");SceneManager.MoveGameObjectToScene(owner,scene);Undo.RegisterCreatedObjectUndo(owner,"Bind match stage");
            var stage=owner.AddComponent<MatchPresentationStage>();stage.Configure(flow,duel,hud,gate,cinematic,camera,player,bot,meshes,material);
            EditorUtility.SetDirty(stage);EditorSceneManager.MarkSceneDirty(scene);return stage;
        }
        // URP discards light bits absent from the project's named rendering layers.
        // Run even when a stage owner already exists: old scenes may lack this setting.
        public static void EnsureStageRenderingLayer()
        {
            const int index=7;
            if((RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue()&MatchPresentationStage.StageRenderingLayer)!=0)return;
            while(RenderingLayerMask.GetLastDefinedRenderingLayerIndex()<index)
            {
                int next=RenderingLayerMask.GetLastDefinedRenderingLayerIndex()+1;
                string name=next==index?"Elemental Match Stage":"Elemental Reserved "+next;
                if(!UnityEditor.Rendering.RenderPipelineEditorUtility.TryAddRenderingLayerName(name))
                    throw new InvalidOperationException("Cannot define the project's stage rendering layer.");
            }
            if((RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue()&MatchPresentationStage.StageRenderingLayer)==0&&
                !UnityEditor.Rendering.RenderPipelineEditorUtility.TrySetRenderingLayerName(index,"Elemental Match Stage"))
                throw new InvalidOperationException("Stage rendering layer bit 7 remains undefined.");
            AssetDatabase.SaveAssets();
        }
    }
}
