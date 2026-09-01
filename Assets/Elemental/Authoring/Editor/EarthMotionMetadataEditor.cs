using System;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Characters;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Catalog-only correction and timeline preview. This window never mutates FBX importers.
    /// </summary>
    public sealed class EarthMotionMetadataEditor : EditorWindow
    {
        private EarthMotionCatalog _catalog;
        private GameObject _previewTarget;
        private SerializedObject _serializedCatalog;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _filter = string.Empty;
        private int _selectedIndex = -1;
        private float _previewPhase01;
        private bool _previewActive;
        private bool _startedAnimationMode;

        [MenuItem("Elemental Suite/Animation/Earth Motion Metadata")]
        public static void Open() =>
            GetWindow<EarthMotionMetadataEditor>("Earth Motion Metadata");

        private void OnEnable()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<EarthMotionCatalog>(
                EarthMotionCatalogBuilder.DefaultCatalogPath);
            RebindCatalog();
        }

        private void OnDisable() => StopPreview();

        private void OnGUI()
        {
            DrawToolbar();
            if (_catalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or build the deterministic EarthMotionCatalog.",
                    MessageType.Info);
                return;
            }

            _serializedCatalog.Update();
            EditorGUILayout.BeginHorizontal();
            DrawClipList();
            DrawSelectedClip();
            EditorGUILayout.EndHorizontal();
            if (_serializedCatalog.ApplyModifiedProperties())
                EditorUtility.SetDirty(_catalog);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            EarthMotionCatalog next = (EarthMotionCatalog)EditorGUILayout.ObjectField(
                _catalog,
                typeof(EarthMotionCatalog),
                false,
                GUILayout.MinWidth(180f));
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                _catalog = next;
                _selectedIndex = -1;
                RebindCatalog();
            }
            _filter = GUILayout.TextField(
                _filter,
                GUI.skin.FindStyle("ToolbarSearchTextField"),
                GUILayout.MinWidth(120f));
            if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton))
            {
                if (_catalog == null)
                    EarthMotionCatalogBuilder.BuildOrUpdateDefaultCatalog();
                else
                {
                    EarthMotionCatalogBuilder.Rebuild(_catalog);
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                }
                _catalog = AssetDatabase.LoadAssetAtPath<EarthMotionCatalog>(
                    EarthMotionCatalogBuilder.DefaultCatalogPath) ?? _catalog;
                RebindCatalog();
            }
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
            {
                var errors = new List<string>();
                EarthMotionCatalogValidationIssue issues =
                    EarthMotionCatalogValidator.Validate(_catalog, errors);
                if (issues == EarthMotionCatalogValidationIssue.None)
                    Debug.Log("[Elemental] Earth motion catalog validation passed.", _catalog);
                else
                    Debug.LogError(string.Join("\n", errors), _catalog);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawClipList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250f));
            EditorGUILayout.LabelField(
                $"Curated clips: {_catalog.ClipCount}/{EarthMotionCatalog.ExpectedCuratedClipCount}",
                EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            for (int index = 0; index < _catalog.ClipCount; index++)
            {
                EarthMotionClipProfile profile = _catalog.ClipAt(index);
                if (profile?.Clip == null ||
                    (!string.IsNullOrWhiteSpace(_filter) &&
                     profile.Clip.name.IndexOf(
                         _filter,
                         StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                bool selected = index == _selectedIndex;
                if (GUILayout.Toggle(
                        selected,
                        $"{index + 1:00}  {profile.Clip.name}",
                        "Button") && !selected)
                {
                    _selectedIndex = index;
                    _previewPhase01 = 0f;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedClip()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            SerializedProperty clips = _serializedCatalog.FindProperty("clips");
            if (_selectedIndex < 0 || _selectedIndex >= clips.arraySize)
            {
                EditorGUILayout.HelpBox(
                    "Select a clip to inspect its provenance, eight curves, windows, and manual corrections.",
                    MessageType.None);
            }
            else
            {
                SerializedProperty entry = clips.GetArrayElementAtIndex(_selectedIndex);
                EarthMotionClipProfile profile = _catalog.ClipAt(_selectedIndex);
                DrawTimeline(profile);
                EditorGUILayout.Space();
                DrawCorrectionFields(entry);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawCorrectionFields(SerializedProperty entry)
        {
            SerializedProperty correctionMask =
                entry.FindPropertyRelative("manualCorrections");
            EditorGUILayout.LabelField("Immutable provenance", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                Draw(entry, "clip");
                Draw(entry, "assetGuid");
                Draw(entry, "localFileId");
                Draw(entry, "sourceAssetPath");
                Draw(entry, "provenance");
                Draw(entry, "provenanceLabel");
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(correctionMask);
            EditorGUILayout.HelpBox(
                "Changing a group marks its correction bit so deterministic rebuilds preserve it.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            Draw(entry, "semanticAction");
            Draw(entry, "authoredAction");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.SemanticAction,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "averageSpeedMetersPerSecond");
            Draw(entry, "planarDirection");
            Draw(entry, "averageYawDegreesPerSecond");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.Kinematics,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "stance");
            Draw(entry, "style");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.StanceAndStyle,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "leftFootContact");
            Draw(entry, "rightFootContact");
            Draw(entry, "leftFootPhase");
            Draw(entry, "rightFootPhase");
            Draw(entry, "landingContact");
            Draw(entry, "safeExit");
            Draw(entry, "pelvisCompression");
            Draw(entry, "rootEffort");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.ContactCurves,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "landingContactPhase01");
            Draw(entry, "safeExitWindow");
            Draw(entry, "cancelWindow");
            Draw(entry, "recoveryWindow");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.Windows,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "handOccupancy");
            Draw(entry, "supportsMirroring");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.HandAndMirroring,
                EditorGUI.EndChangeCheck());

            EditorGUI.BeginChangeCheck();
            Draw(entry, "environmentTags");
            Draw(entry, "actionTags");
            MarkChanged(
                correctionMask,
                EarthMotionManualCorrection.Tags,
                EditorGUI.EndChangeCheck());
        }

        private static void Draw(SerializedProperty entry, string relativeName) =>
            EditorGUILayout.PropertyField(entry.FindPropertyRelative(relativeName), true);

        private static void MarkChanged(
            SerializedProperty correctionMask,
            EarthMotionManualCorrection correction,
            bool changed)
        {
            if (changed) correctionMask.intValue |= (int)correction;
        }

        private void DrawTimeline(EarthMotionClipProfile profile)
        {
            EditorGUILayout.LabelField("Timeline preview", EditorStyles.boldLabel);
            _previewTarget = (GameObject)EditorGUILayout.ObjectField(
                "Preview target",
                _previewTarget,
                typeof(GameObject),
                true);
            EditorGUI.BeginChangeCheck();
            _previewPhase01 = EditorGUILayout.Slider(
                "Normalized phase",
                _previewPhase01,
                0f,
                1f);
            if (EditorGUI.EndChangeCheck() && _previewActive)
                SamplePreview(profile);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_previewTarget == null || profile?.Clip == null))
            {
                if (GUILayout.Button(_previewActive ? "Resample" : "Start preview"))
                    SamplePreview(profile);
            }
            if (GUILayout.Button("Stop preview")) StopPreview();
            EditorGUILayout.EndHorizontal();

            if (profile == null) return;
            for (int index = 0; index < EarthAnimationClipMetadata.CurveCount; index++)
            {
                AnimationCurve curve = profile.Curve(index);
                float value = curve != null && curve.length > 0
                    ? Mathf.Clamp01(curve.Evaluate(_previewPhase01))
                    : 0f;
                EditorGUILayout.LabelField(
                    EarthAnimationClipMetadata.CurveName(index),
                    value.ToString("0.000"));
            }
        }

        private void SamplePreview(EarthMotionClipProfile profile)
        {
            if (_previewTarget == null || profile?.Clip == null) return;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                _startedAnimationMode = true;
            }
            _previewActive = true;
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(
                    _previewTarget,
                    profile.Clip,
                    _previewPhase01 * Mathf.Max(0.0001f, profile.Clip.length));
            }
            finally
            {
                AnimationMode.EndSampling();
            }
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            if (_startedAnimationMode && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            _previewActive = false;
            _startedAnimationMode = false;
        }

        private void RebindCatalog() =>
            _serializedCatalog = _catalog != null ? new SerializedObject(_catalog) : null;
    }
}
