using Elemental.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class StoneHudExactInstaller
    {
        public const string ExactPath="Assets/Elemental/Content/UI/Stone/StoneHudExactProfile.asset";
        public const string LayoutPath="Assets/Elemental/Content/UI/Stone/StoneHudExactLayout.asset";
        public const string ReferencePath="Assets/Elemental/Content/UI/Stone/StoneReferenceProfile.asset";
        [MenuItem("Elemental/UI/Install Exact Reference HUD")]
        public static void Install()
        {
            var reference=AssetDatabase.LoadAssetAtPath<ElementalStoneReferenceProfile>(ReferencePath);
            if(reference==null)throw new System.InvalidOperationException("Install Stone Artwork first; existing reference profile is required.");
            var exact=AssetDatabase.LoadAssetAtPath<StoneHudExactProfile>(ExactPath);
            if(exact==null){exact=ScriptableObject.CreateInstance<StoneHudExactProfile>();AssetDatabase.CreateAsset(exact,ExactPath);}
            var layout=AssetDatabase.LoadAssetAtPath<ElementalHudLayout>(LayoutPath);
            if(layout==null)
            {
                layout=reference.hudLayout!=null?Object.Instantiate(reference.hudLayout):ScriptableObject.CreateInstance<ElementalHudLayout>();
                layout.name="StoneHudExactLayout";AssetDatabase.CreateAsset(layout,LayoutPath);
            }
            Undo.RecordObjects(new Object[]{reference,exact,layout},"Install exact reference HUD");
            StoneHudExactProfile.ApplyLayout(layout);exact.enabled=true;reference.exactHud=exact;reference.hudLayout=layout;
            EditorUtility.SetDirty(exact);EditorUtility.SetDirty(layout);EditorUtility.SetDirty(reference);
            AssetDatabase.SaveAssets();
            Debug.Log("Exact HUD bound: live score, health, mana, selection and globe retained. Original StoneReferenceHudLayout asset was not modified.");
        }
    }
}
