using Elemental.Presentation.UI;
using UnityEditor;
using UnityEngine;
namespace Elemental.Authoring.Editor
{
    [CustomEditor(typeof(MenuScreenLayout))]
    public sealed class MenuScreenLayoutEditor : UnityEditor.Editor
    {
        private string filter="";
        private bool technical;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Настройки меню: "+((MenuScreenLayout)target).screen,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Масштаб 1 = исходный. Размер / шрифт 0 = исходный. Отступ −1 = исходный. Изменения видны во время Play. В боковом меню Y направлен вверх, на экране результата — вниз.",MessageType.Info);
            var layout=(MenuScreenLayout)target;
            var elements=serializedObject.FindProperty("elements");
            if(layout.screen==MenuScreenId.Sidebar||layout.screen==MenuScreenId.Victory||layout.screen==MenuScreenId.Defeat||layout.screen==MenuScreenId.Draw)
            {
                string[] keys={"glowStrength","glowRadius","particleCount","particleSize","particleTravel","chromaticPixels","chromaticOpacity","positiveFringe","negativeFringe"};
                string[] labels={"Сила свечения","Радиус свечения","Количество частиц","Размер частиц: мин / макс","Дальность движения частиц","Разделение цветных краёв, px","Видимость цветных краёв","Правый цветной край","Левый цветной край"};
                for(int i=0;i<keys.Length;i++)EditorGUILayout.PropertyField(serializedObject.FindProperty(keys[i]),new GUIContent(labels[i]));
            }
            EditorGUILayout.Space();filter=EditorGUILayout.TextField("Поиск блока",filter);
            technical=EditorGUILayout.Toggle("Показать технические пути",technical);
            for(int i=0;i<elements.arraySize;i++)
            {
                var node=elements.GetArrayElementAtIndex(i);string path=node.FindPropertyRelative("path").stringValue;
                string title=node.FindPropertyRelative("displayName").stringValue;
                if(string.IsNullOrWhiteSpace(title))title=MenuLayoutNames.Friendly(path,layout.screen);
                if(filter.Length>0&&title.IndexOf(filter,System.StringComparison.OrdinalIgnoreCase)<0&&path.IndexOf(filter,System.StringComparison.OrdinalIgnoreCase)<0)continue;
                node.isExpanded=EditorGUILayout.Foldout(node.isExpanded,title,true);
                if(!node.isExpanded)continue;
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(node.FindPropertyRelative("displayName"),new GUIContent("Моё название блока"));
                if(technical)EditorGUILayout.SelectableLabel(path,EditorStyles.miniLabel,GUILayout.Height(32));
                string[] fields={"offset","size","scale","rotation","fontSize","padding"};
                string[] names={"Положение: X / Y","Размер: ширина / высота","Масштаб: X / Y","Поворот, °","Размер текста","Отступы: слева / сверху / справа / снизу"};
                for(int n=0;n<fields.Length;n++)EditorGUILayout.PropertyField(node.FindPropertyRelative(fields[n]),new GUIContent(names[n]));
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
