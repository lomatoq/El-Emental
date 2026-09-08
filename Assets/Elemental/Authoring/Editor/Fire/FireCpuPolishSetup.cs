using Elemental.Presentation.Fire;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor.Fire
{
    public static class FireCpuPolishSetup
    {
        // Explicit isolated profile migration; graph rebuilds do not silently reset art tuning.
        [MenuItem("Elemental/Fire/Apply CPU Flame Polish Candidate")]
        public static void Apply()
        {
            const string folder="Assets/Elemental/Content/VFX/Fire/";
            var profile=AssetDatabase.LoadAssetAtPath<FireVisualProfile>(folder+"Fire_Default.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Fire_CpuMesh.mat");
            if(profile==null || material==null) throw new System.InvalidOperationException("Build Fire graphs and profile first.");
            Undo.RecordObjects(new Object[]{profile,material},"Apply CPU fire visual candidate");
            profile.FlameMinWidth=0.30f; profile.FlameMaxWidth=0.58f;
            profile.FlameMinAspect=1.3f; profile.FlameMaxAspect=2.1f;
            material.SetFloat("_CoreEmission",0.65f); material.SetFloat("_Opacity",0.72f);
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        }
    }
}
