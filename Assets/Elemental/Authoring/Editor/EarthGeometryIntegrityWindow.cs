using System;
using System.Collections.Generic;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public sealed class EarthGeometryIntegrityWindow : EditorWindow
    {
        private static readonly List<SceneMeshResult> Results = new List<SceneMeshResult>(256);
        private static bool _overlayEnabled;
        private static bool _showValid;
        private static Vector2 _scroll;
        private static int _validCount;
        private static int _invalidCount;

        [MenuItem("Elemental/Diagnostics/Geometry Integrity View")]
        public static void Open()
        {
            var window = GetWindow<EarthGeometryIntegrityWindow>();
            window.titleContent = new GUIContent("Earth Geometry Integrity");
            window.minSize = new Vector2(680f, 380f);
            ScanOpenScenes();
            window.Show();
        }

        [Shortcut("Elemental/Toggle Geometry Integrity View", KeyCode.F8)]
        private static void ToggleShortcut()
        {
            _overlayEnabled = !_overlayEnabled;
            if (_overlayEnabled) ScanOpenScenes();
            SceneView.RepaintAll();
            foreach (EarthGeometryIntegrityWindow window in Resources.FindObjectsOfTypeAll<EarthGeometryIntegrityWindow>())
                window.Repaint();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            ScanOpenScenes();
        }

        private void OnDisable() => SceneView.duringSceneGui -= DuringSceneGui;

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan open scene", EditorStyles.toolbarButton)) ScanOpenScenes();
                bool overlay = GUILayout.Toggle(_overlayEnabled, "F8 overlay", EditorStyles.toolbarButton);
                if (overlay != _overlayEnabled)
                {
                    _overlayEnabled = overlay;
                    SceneView.RepaintAll();
                }
                _showValid = GUILayout.Toggle(_showValid, "Show valid", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"VALID {_validCount}   BLOCKED {_invalidCount}", EditorStyles.boldLabel);
            }

            EditorGUILayout.HelpBox(
                "Red geometry is blocked by the V4.1 publication court. Orange is structurally valid but mirrored by a negative transform. " +
                "The validator never hides mixed winding with a blind RecalculateNormals pass.",
                _invalidCount > 0 ? MessageType.Error : MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int index = 0; index < Results.Count; index++)
            {
                SceneMeshResult result = Results[index];
                if (result.Report.IsValid && !_showValid) continue;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIStyle style = result.Report.IsValid ? EditorStyles.label : EditorStyles.boldLabel;
                        GUILayout.Label(result.Owner != null ? result.Owner.name : "<destroyed>", style);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(result.Report.IsValid ? "VALID" : "BLOCKED", style);
                    }
                    EditorGUILayout.LabelField(result.Report.ToString(), EditorStyles.wordWrappedMiniLabel);
                    if (result.Owner != null && GUILayout.Button("Select and frame"))
                    {
                        Selection.activeObject = result.Owner;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public static void ScanOpenScenes()
        {
            Results.Clear();
            _validCount = 0;
            _invalidCount = 0;
            MeshFilter[] filters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
            for (int index = 0; index < filters.Length; index++)
                Add(filters[index], filters[index].sharedMesh);

            SkinnedMeshRenderer[] skinned = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            for (int index = 0; index < skinned.Length; index++)
                Add(skinned[index], skinned[index].sharedMesh);

            MeshCollider[] colliders = FindObjectsByType<MeshCollider>(FindObjectsInactive.Include);
            for (int index = 0; index < colliders.Length; index++)
                Add(colliders[index], colliders[index].sharedMesh, colliders[index].convex);

            Results.Sort((left, right) =>
            {
                int validity = left.Report.IsValid.CompareTo(right.Report.IsValid);
                return validity != 0 ? validity : string.Compare(left.Owner.name, right.Owner.name, StringComparison.Ordinal);
            });
            SceneView.RepaintAll();
        }

        private static void Add(Component owner, Mesh mesh, bool convexCollider = false)
        {
            if (owner == null || mesh == null) return;
            EarthMeshIntegrityPolicy policy = convexCollider
                ? EarthMeshIntegrityPolicy.ConvexCollider
                : IsClosedEarthObject(owner.name)
                    ? EarthMeshIntegrityPolicy.ClosedHero
                    : EarthMeshIntegrityPolicy.OpenVisualSurface;
            EarthMeshIntegrityReport report = EarthMeshIntegrityValidator.Validate(
                mesh,
                policy,
                owner.transform.localToWorldMatrix);
            Results.Add(new SceneMeshResult(owner, mesh, report));
            if (report.IsValid) _validCount++;
            else _invalidCount++;
        }

        private static bool IsClosedEarthObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            string value = objectName.ToLowerInvariant();
            return value.Contains("earth") || value.Contains("stone") || value.Contains("rock") ||
                   value.Contains("wall") || value.Contains("platform") || value.Contains("armor") ||
                   value.Contains("fragment") || value.Contains("pillar") || value.Contains("meteor");
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            if (!_overlayEnabled) return;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int index = 0; index < Results.Count; index++)
            {
                SceneMeshResult result = Results[index];
                if (result.Owner == null || result.Mesh == null || result.Report.IsValid) continue;
                Transform transform = result.Owner.transform;
                Bounds bounds = result.Mesh.bounds;
                Color color = (result.Report.Issues & EarthMeshIntegrityIssue.NegativeTransformDeterminant) != 0
                    ? new Color(1f, 0.45f, 0.05f, 0.95f)
                    : new Color(1f, 0.08f, 0.08f, 0.95f);
                using (new Handles.DrawingScope(color, transform.localToWorldMatrix))
                {
                    Handles.DrawWireCube(bounds.center, bounds.size);
                    Handles.Label(bounds.center + Vector3.up * bounds.extents.y, result.Report.Issues.ToString());
                }
            }

            Handles.BeginGUI();
            GUI.Box(new Rect(14f, 14f, 260f, 46f), $"GEOMETRY INTEGRITY  F8\n{_validCount} valid / {_invalidCount} blocked");
            Handles.EndGUI();
        }

        private readonly struct SceneMeshResult
        {
            public SceneMeshResult(Component owner, Mesh mesh, EarthMeshIntegrityReport report)
            {
                Owner = owner;
                Mesh = mesh;
                Report = report;
            }
            public Component Owner { get; }
            public Mesh Mesh { get; }
            public EarthMeshIntegrityReport Report { get; }
        }
    }
}
