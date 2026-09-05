using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    [CustomEditor(typeof(EarthPillarWaveProfile), true), CanEditMultipleObjects]
    public sealed class EarthPillarWaveProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Волна", EditorStyles.boldLabel);
            DrawWaveLength();
            EditorGUILayout.HelpBox("Кривые: X — время фазы от 0 до 1, Y — высота относительно максимума. Концы фаз соединяются автоматически.", MessageType.None);
            DrawPhase("Подготовка", "anticipationCurve", "precompressionSeconds", .01f, 2f);
            DrawPhase("Подъём", "riseCurve", "premiumRiseSeconds", .05f, 5f);
            DrawPhase("Оседание", "settleCurve", "premiumSettleSeconds", .01f, 3f);
            DrawPhase("Удержание", "holdCurve", "premiumHoldSeconds", 0f, 5f);
            DrawPhase("Уход в землю", "retreatCurve", "premiumRetreatSeconds", .05f, 5f);
            EditorGUILayout.LabelField("Наклон отдельных гребней — за весь цикл", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltCurve"), GUIContent.none, GUILayout.Height(40f));
            EditorGUILayout.LabelField("Y: −1…1 × угол наклона", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Общий фракчер движется без вращения. Наклон применяется к отдельным гребням. Ряды поднимаются с перекрытием: задержка = расстояние / скорость. Длинные фазы делают волну шире, не замедляя её.", MessageType.None);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Пять основных параметров", EditorStyles.boldLabel);
            Number("crestHeight", "Высота, м", .1f, 6f);
            Number("maximumDistance", "Дальность, м", 2.5f, 20f);
            Number("waveSpeed", "Скорость гребня, м/с", .5f, 15f);
            Number("maximumImpulse", "Сила толчка", 0f, 1000f);
            Number("premiumTiltDegrees", "Угол наклона, °", 0f, 20f);
            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.FindProperty("motionMode").enumValueIndex = 1;
                serializedObject.FindProperty("minimumImpulse").floatValue = serializedObject.FindProperty("maximumImpulse").floatValue * .1f;
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWaveLength()
        {
            float speed = Mathf.Max(.1f, serializedObject.FindProperty("waveSpeed").floatValue);
            string[] phases = {"premiumRiseSeconds", "premiumSettleSeconds", "premiumHoldSeconds", "premiumRetreatSeconds"};
            float seconds = 0f;
            foreach (string phase in phases) seconds += serializedObject.FindProperty(phase).floatValue;
            EditorGUI.BeginChangeCheck();
            float length = EditorGUILayout.FloatField(new GUIContent("Длина фазы волны, м",
                "Протяжённость полного подъёма и спада. Изменение пропорционально растягивает тайминги ниже; скорость и форма кривых сохраняются."), seconds * speed);
            if (EditorGUI.EndChangeCheck() && float.IsFinite(length))
            {
                float requestedSeconds = Mathf.Max(.1f, length) / speed;
                float factor = requestedSeconds / Mathf.Max(.001f, seconds);
                var timing = new Elemental.Simulation.Bending.EarthWaveAnimationTiming(
                    serializedObject.FindProperty("precompressionSeconds").floatValue,
                    serializedObject.FindProperty(phases[0]).floatValue * factor,
                    serializedObject.FindProperty(phases[1]).floatValue * factor,
                    serializedObject.FindProperty(phases[2]).floatValue * factor,
                    serializedObject.FindProperty(phases[3]).floatValue * factor);
                serializedObject.FindProperty(phases[0]).floatValue = timing.Rise;
                serializedObject.FindProperty(phases[1]).floatValue = timing.Settle;
                serializedObject.FindProperty(phases[2]).floatValue = timing.Hold;
                serializedObject.FindProperty(phases[3]).floatValue = timing.Retreat;
                seconds = timing.Duration;
            }
            EditorGUILayout.LabelField($"Полный подъём и спад: {seconds:0.##} сек при {speed:0.##} м/с", EditorStyles.miniLabel);
            EditorGUILayout.Space(5f);
        }

        private void DrawPhase(string label, string curve, string seconds, float minimum, float maximum)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var timing = serializedObject.FindProperty(seconds);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(timing.floatValue, GUILayout.Width(62f));
            if (EditorGUI.EndChangeCheck()) timing.floatValue = Mathf.Clamp(value, minimum, maximum);
            EditorGUILayout.LabelField("сек", GUILayout.Width(24f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(curve), GUIContent.none, GUILayout.Height(38f));
        }
        private void Number(string property, string label, float minimum, float maximum)
        {
            var field = serializedObject.FindProperty(property);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(label, field.floatValue, minimum, maximum);
            if (EditorGUI.EndChangeCheck()) field.floatValue = value;
        }
    }
}
