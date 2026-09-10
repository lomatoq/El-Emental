using System;
using System.Linq;
using Elemental.Presentation.Fire;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace Elemental.Authoring.Editor
{
    public static class FireFollowupInstaller
    {
        public static void ApplyCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install in idle Edit mode.");
            var scene=SceneManager.GetActiveScene();
            if(!scene.path.EndsWith("EarthCoreSlice.unity"))throw new InvalidOperationException("Select saved production EarthCoreSlice scene.");
            var roots=scene.GetRootGameObjects();
            var presenter=roots.SelectMany(r=>r.GetComponentsInChildren<EarthMaterialFeedbackPresenter>(true)).First();
            var profile=(EarthEffectsTuningProfile)new SerializedObject(presenter).FindProperty("profile").objectReferenceValue;
            if(profile==null||profile.Materials.ImpactDust==null)throw new InvalidOperationException("Missing production smoke/dust source material.");
            foreach(var binding in roots.SelectMany(r=>r.GetComponentsInChildren<FireStreamPresentationBinding>(true)))
            {var serialized=new SerializedObject(binding);serialized.FindProperty("smolderSmokeMaterial").objectReferenceValue=profile.Materials.ImpactDust;serialized.ApplyModifiedProperties();EditorUtility.SetDirty(binding);}
            OpponentSizeParityInstaller.ApplyCurrentScene();EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
