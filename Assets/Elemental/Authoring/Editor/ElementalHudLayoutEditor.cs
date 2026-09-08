using Elemental.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    [CustomEditor(typeof(ElementalHudLayout))]
    public sealed class ElementalHudLayoutEditor : UnityEditor.Editor
    {
        public const string LayoutPath = "Assets/Elemental/Content/UI/Frontend/ElementalHudLayout.asset";
        [MenuItem("Elemental/UI/Edit HUD Layout")]
        public static void SelectLayout()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ElementalUITheme>(AlphaFrontendSetup.ThemePath);
            if (theme == null) throw new System.InvalidOperationException("Install the alpha frontend before editing its layout.");
            if (theme.hudLayout == null)
            {
                var layout = AssetDatabase.LoadAssetAtPath<ElementalHudLayout>(LayoutPath);
                if (layout == null) { layout = CreateInstance<ElementalHudLayout>(); AssetDatabase.CreateAsset(layout, LayoutPath); }
                Undo.RecordObject(theme, "Assign HUD layout"); theme.hudLayout = layout;
                EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            }
            Selection.activeObject = theme.hudLayout; EditorGUIUtility.PingObject(theme.hudLayout);
        }
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Group = вся группа. Under Bar = иконка и число под шкалой.\n" +
                "Anchor / Pivot: (0,0) слева сверху, (1,1) справа снизу.\n" +
                "Position: X вправо, Y вниз. Size: размер блока. Scale: масштаб вместе с текстом. Rotation: градусы.\n" +
                "Изменения видны сразу в Play Mode и сохраняются в этом asset.", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("Save HUD Layout / Сохранить"))
            { EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target); }
        }
    }
}
