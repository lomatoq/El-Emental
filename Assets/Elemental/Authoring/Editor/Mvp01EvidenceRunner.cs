using System.IO;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    [InitializeOnLoad]
    internal static class Mvp01EvidenceRunner
    {
        private const string PendingKey = "Elemental.Mvp01Evidence.Pending";
        private const string StatusPath = "BuildReports/Mvp01RescueCurrent.json";
        private static int _settleUpdates;

        static Mvp01EvidenceRunner()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Elemental/QA/Run MVP 0.1 Accepted Evidence")]
        private static void Run()
        {
            string fullStatus = Path.GetFullPath(StatusPath);
            if (File.Exists(fullStatus)) File.Delete(fullStatus);
            SessionState.SetBool(PendingKey, true);
            if (EditorApplication.isPlaying)
            {
                QueueRuntimeStart();
                return;
            }
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(PendingKey, false)) QueueRuntimeStart();
        }

        private static void QueueRuntimeStart()
        {
            _settleUpdates = 0;
            EditorApplication.update -= StartWhenSceneIsReady;
            EditorApplication.update += StartWhenSceneIsReady;
        }

        private static void StartWhenSceneIsReady()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= StartWhenSceneIsReady;
                return;
            }
            if (++_settleUpdates < 12) return;
            VisualQaCaptureBehaviour qa = Object.FindAnyObjectByType<VisualQaCaptureBehaviour>();
            if (qa == null) return;
            EditorApplication.update -= StartWhenSceneIsReady;
            SessionState.EraseBool(PendingKey);
            bool started = qa.BeginMvpRescueEvidence();
            Debug.Log($"[Elemental] Lifecycle-owned MVP evidence started: {started}.");
        }
    }
}
