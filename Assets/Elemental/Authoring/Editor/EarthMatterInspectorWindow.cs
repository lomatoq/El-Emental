using System.Collections.Generic;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Matter;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public sealed class EarthMatterInspectorWindow : EditorWindow
    {
        private readonly List<EarthMatterIdentity> _identities = new List<EarthMatterIdentity>(256);
        private readonly List<MonoBehaviour> _missingTargets = new List<MonoBehaviour>(128);
        private Vector2 _scroll;
        private bool _showConsumed;

        [MenuItem("Elemental/Diagnostics/Earth Matter Inspector")]
        public static void Open()
        {
            EarthMatterInspectorWindow window = GetWindow<EarthMatterInspectorWindow>();
            window.titleContent = new GUIContent("Earth Matter");
            window.minSize = new Vector2(720f, 420f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable() => Refresh();

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
                _showConsumed = GUILayout.Toggle(_showConsumed, "Show consumed", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Records {_identities.Count}   Missing {_missingTargets.Count}");
            }
            EditorGUILayout.HelpBox(
                "Every targetable Earth object must have a stable id, provenance, volume/mass, representation tier and owner. " +
                "A missing row is a Gate 1 failure.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int index = 0; index < _missingTargets.Count; index++)
            {
                MonoBehaviour target = _missingTargets[index];
                if (target == null) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label($"MISSING MATTER RECORD  {target.name} ({target.GetType().Name})", EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(80f))) Selection.activeObject = target.gameObject;
                }
            }
            for (int index = 0; index < _identities.Count; index++)
            {
                EarthMatterIdentity identity = _identities[index];
                if (identity == null) continue;
                bool found = identity.TryRead(out EarthMatterRecord record);
                if (found && !_showConsumed && record.Phase == EarthMatterPhase.Consumed) continue;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(identity.name, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(found ? record.Id.ToString() : "UNREGISTERED", EditorStyles.boldLabel);
                    }
                    if (!found)
                    {
                        EditorGUILayout.HelpBox("Identity has no live registry record.", MessageType.Error);
                        continue;
                    }
                    EditorGUILayout.LabelField(
                        $"{record.Phase} / {record.Representation} / {record.Shape} / {record.Material}");
                    EditorGUILayout.LabelField(
                        $"volume {record.Volume:0.0000} m³  mass {record.Mass:0.0} kg  integrity {record.Integrity:P0}");
                    EditorGUILayout.LabelField(
                        $"source {record.Source.Kind}:{record.Source.SourceStableId}:{record.Source.SourceGeneration} " +
                        $"cell {record.Source.SourceCellIndex} rev {record.Source.SourceRevision} exact={record.Source.CanReturnExactly}");
                    EditorGUILayout.LabelField(
                        $"owner {record.Owner.StableId}:{record.Owner.Generation}  position {record.CurrentPose.Position}");
                    if (GUILayout.Button("Select and frame"))
                    {
                        Selection.activeObject = identity.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            _identities.Clear();
            _missingTargets.Clear();
            _identities.AddRange(FindObjectsByType<EarthMatterIdentity>(FindObjectsInactive.Include));
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour is not IEarthPhysicalTarget target || !target.IsEarthTargetValid) continue;
                if (behaviour.GetComponent<EarthMatterIdentity>() == null)
                    _missingTargets.Add(behaviour);
            }
            Repaint();
        }
    }
}
