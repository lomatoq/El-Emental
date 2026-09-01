using System;
using System.IO;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Rendering.Editor
{
    [InitializeOnLoad]
    public static class Gate1CaptureEditorBootstrap
    {
        private const string ShippingScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string StageKey = "Elemental.Gate1Capture.Stage";
        private const string BatchKey = "Elemental.Gate1Capture.Batch";
        private const string PreviousSceneKey = "Elemental.Gate1Capture.PreviousScene";
        private const string OutputKey = "Elemental.Gate1Capture.Output";
        private const int StageIdle = 0;
        private const int StagePlaying = 1;
        private const int StageManifestReady = 2;
        private static readonly string RequestFilePath = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "../Library/ElementalGate1Capture.request.json"));

        private static bool s_Active;
        private static double s_Deadline;

        static Gate1CaptureEditorBootstrap()
        {
            if (SessionState.GetInt(StageKey, StageIdle) == StageIdle) return;
            EditorApplication.delayCall += ResumeAfterDomainReload;
        }

        [MenuItem("Elemental/QA/Capture Gate1 Transient A-B")]
        public static void RunFromMenu()
        {
            Begin(Path.GetFullPath(Gate1CaptureRequest.DefaultOutputDirectory), false);
        }

        public static void RunBatch()
        {
            if (!Gate1CaptureRequest.TryParse(
                    Environment.GetCommandLineArgs(),
                    out Gate1CaptureRequest request))
            {
                Debug.LogError(
                    $"[Elemental] {Gate1CaptureRequest.Argument} <output-directory> is required.");
                EditorApplication.Exit(6);
                return;
            }
            Begin(Path.GetFullPath(request.OutputDirectory), true);
        }

        private static void Begin(string outputDirectory, bool batch)
        {
            if (s_Active || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Elemental] Gate1 capture cannot start while Play Mode is active.");
                if (batch) EditorApplication.Exit(6);
                return;
            }
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
            {
                Debug.LogError(
                    "[Elemental] Gate1 capture refused to close a dirty scene. Save or revert it explicitly, then rerun.");
                if (batch) EditorApplication.Exit(6);
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            string manifestPath = Path.Combine(
                outputDirectory,
                Gate1CaptureRequest.ManifestFileName);
            if (File.Exists(manifestPath)) File.Delete(manifestPath);
            var requestFile = new Gate1EditorRequestFile
            {
                outputDirectory = outputDirectory,
                expiresUtcTicks = DateTime.UtcNow.AddMinutes(10d).Ticks
            };
            File.WriteAllText(RequestFilePath, JsonUtility.ToJson(requestFile, true));
            SessionState.SetString(
                PreviousSceneKey,
                current.IsValid() ? current.path : string.Empty);
            SessionState.SetString(OutputKey, outputDirectory);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetInt(StageKey, StagePlaying);
            EditorSceneManager.OpenScene(ShippingScenePath, OpenSceneMode.Single);
            if (SceneManager.GetActiveScene().isDirty)
            {
                FailBeforePlay("The shipping scene was dirty immediately after opening.", batch);
                return;
            }
            ActivatePolling();
            EditorApplication.EnterPlaymode();
        }

        private static void ResumeAfterDomainReload()
        {
            ActivatePolling();
        }

        private static void ActivatePolling()
        {
            if (s_Active) return;
            s_Active = true;
            s_Deadline = EditorApplication.timeSinceStartup + 300d;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Poll()
        {
            if (!s_Active) return;
            string outputDirectory = SessionState.GetString(OutputKey, string.Empty);
            string manifestPath = Path.Combine(
                outputDirectory,
                Gate1CaptureRequest.ManifestFileName);
            if (TryReadCompleteManifest(manifestPath, out _))
            {
                SessionState.SetInt(StageKey, StageManifestReady);
                if (EditorApplication.isPlaying)
                    EditorApplication.ExitPlaymode();
                else
                    FinalizeAndExit();
                return;
            }
            if (EditorApplication.timeSinceStartup <= s_Deadline) return;
            Debug.LogError("[Elemental] Gate1 transient A/B capture timed out.");
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            else
                Finish(false);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!s_Active || state != PlayModeStateChange.EnteredEditMode) return;
            FinalizeAndExit();
        }

        private static void FinalizeAndExit()
        {
            bool manifestReady = SessionState.GetInt(StageKey, StageIdle) ==
                StageManifestReady;
            string outputDirectory = SessionState.GetString(OutputKey, string.Empty);
            string manifestPath = Path.Combine(
                outputDirectory,
                Gate1CaptureRequest.ManifestFileName);
            bool shippingSceneClean =
                SceneManager.GetActiveScene().path == ShippingScenePath &&
                !SceneManager.GetActiveScene().isDirty;
            Gate1CaptureManifest manifest = null;
            bool succeeded = manifestReady &&
                TryReadCompleteManifest(manifestPath, out manifest);
            if (manifest != null)
            {
                manifest.restoration.editorSceneWasCleanBeforePlay = true;
                manifest.restoration.editorSceneCleanAfterPlay = shippingSceneClean;
                if (!shippingSceneClean)
                {
                    manifest.success = false;
                    manifest.failure =
                        "The shipping scene became dirty during transient Gate1 capture.";
                }
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                succeeded &= manifest.success && shippingSceneClean;
            }
            Finish(succeeded);
        }

        private static void Finish(bool succeeded)
        {
            bool batch = SessionState.GetBool(BatchKey, Application.isBatchMode);
            string previousScene = SessionState.GetString(PreviousSceneKey, string.Empty);
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            s_Active = false;
            SessionState.SetInt(StageKey, StageIdle);
            SessionState.SetBool(BatchKey, false);
            SessionState.SetString(PreviousSceneKey, string.Empty);
            SessionState.SetString(OutputKey, string.Empty);
            if (File.Exists(RequestFilePath)) File.Delete(RequestFilePath);
            if (!batch && !string.IsNullOrWhiteSpace(previousScene) &&
                File.Exists(Path.GetFullPath(previousScene)) &&
                previousScene != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            if (batch)
                EditorApplication.Exit(succeeded ? 0 : 5);
            else if (succeeded)
                Debug.Log("[Elemental] Gate1 transient A/B capture completed and the prior scene was restored.");
            else
                Debug.LogError("[Elemental] Gate1 transient A/B capture did not satisfy its evidence gate.");
        }

        private static void FailBeforePlay(string failure, bool batch)
        {
            Debug.LogError($"[Elemental] Gate1 capture aborted: {failure}");
            if (File.Exists(RequestFilePath)) File.Delete(RequestFilePath);
            SessionState.SetInt(StageKey, StageIdle);
            if (batch) EditorApplication.Exit(6);
        }

        private static bool TryReadCompleteManifest(
            string path,
            out Gate1CaptureManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                new FileInfo(path).Length == 0)
                return false;
            try
            {
                manifest = JsonUtility.FromJson<Gate1CaptureManifest>(
                    File.ReadAllText(path));
                return manifest != null && manifest.complete;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Elemental] Gate1 manifest could not be read: {exception.Message}");
                return false;
            }
        }

        [Serializable]
        private sealed class Gate1EditorRequestFile
        {
            public string outputDirectory;
            public long expiresUtcTicks;
        }
    }
}
