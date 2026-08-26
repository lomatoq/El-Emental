using System;
using System.IO;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Lets the existing player-style visual court run directly in a batch Unity
    /// Editor. This keeps visual verification fast and avoids producing a Windows
    /// build solely for captures.
    /// </summary>
    [InitializeOnLoad]
    public static class VisualQaEditorBootstrap
    {
        private const string StageKey = "Elemental.VisualQa.EditorStage";
        private const int StageInitial = 0;
        private const int StageWaitingForEdit = 1;
        private const int StageRunningLab = 2;
        private const int StageComplete = 3;

        private static VisualQaCaptureRequest _request;
        private static double _deadline;
        private static bool _active;
        private static bool _useShippingScene;

        static VisualQaEditorBootstrap()
        {
            if (!Application.isBatchMode ||
                !VisualQaCaptureRequest.TryParse(Environment.GetCommandLineArgs(), out _request))
                return;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        VisualQaCaptureRequest.ShippingSceneArgument,
                        StringComparison.OrdinalIgnoreCase)) continue;
                _useShippingScene = true;
                break;
            }
            EditorApplication.delayCall += Begin;
        }

        private static void Begin()
        {
            if (_active) return;
            _active = true;
            _deadline = EditorApplication.timeSinceStartup + 240d;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            int stage = SessionState.GetInt(StageKey, StageInitial);
            if (stage == StageComplete)
            {
                Exit(true);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // A test run can leave the next Editor process restoring a backup
                // scene in Play Mode. StageRunningLab is our own domain reload;
                // any other Play state must be left before opening the QA scene.
                if (stage != StageRunningLab)
                {
                    SessionState.SetInt(StageKey, StageWaitingForEdit);
                    EditorApplication.ExitPlaymode();
                }
                return;
            }
            if (stage == StageRunningLab)
            {
                Exit(false);
                return;
            }
            LaunchScene();
        }

        private static void LaunchScene()
        {
            EditorSceneManager.OpenScene(
                _useShippingScene
                    ? M3EarthCoreSetup.EarthCoreScenePath
                    : EarthPolishLabSetup.ScenePath);
            SessionState.SetInt(StageKey, StageRunningLab);
            EditorApplication.EnterPlaymode();
        }

        private static void Poll()
        {
            if (!_active) return;
            string path = Path.GetFullPath(_request.OutputPath);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                SessionState.SetInt(StageKey, StageComplete);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else Exit(true);
                return;
            }
            if (EditorApplication.timeSinceStartup <= _deadline) return;
            Debug.LogError($"[Elemental] Editor visual QA timed out: {_request.Scenario}.");
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else Exit(false);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!_active || state != PlayModeStateChange.EnteredEditMode) return;
            int stage = SessionState.GetInt(StageKey, StageInitial);
            if (stage == StageWaitingForEdit)
            {
                LaunchScene();
                return;
            }
            Exit(stage == StageComplete);
        }

        private static void Exit(bool completed)
        {
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _active = false;
            SessionState.SetInt(StageKey, StageInitial);
            EditorApplication.Exit(completed ? 0 : 5);
        }
    }
}
