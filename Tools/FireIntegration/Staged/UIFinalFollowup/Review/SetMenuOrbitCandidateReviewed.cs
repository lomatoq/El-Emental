// Tools-only: replay as Unity_RunCommand.Code in Edit mode with EarthCoreSlice active.
using System;
using Elemental.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply authored menu framing in Edit mode before the capture fixture.");
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "EarthCoreSlice")
            throw new InvalidOperationException("EarthCoreSlice must be the active loaded scene.");
        CinematicMenuCamera owner = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<CinematicMenuCamera>(true))
            {
                if (owner != null) throw new InvalidOperationException("Ambiguous menu camera ownership.");
                owner = candidate;
            }
        if (owner == null) throw new InvalidOperationException("No authored CinematicMenuCamera in this scene.");
        var serialized = new SerializedObject(owner);
        var azimuth = serialized.FindProperty("presentationAzimuth");
        if (azimuth == null) throw new InvalidOperationException("Menu orbit property changed; review before applying.");
        float previous = azimuth.floatValue;
        if (Mathf.Approximately(previous, 0f)) { result.Log("Menu orbit candidate already applied: 0 degrees."); return; }
        if (!Mathf.Approximately(previous, 35f))
            throw new InvalidOperationException("Authored orbit no longer equals reviewed baseline 35 degrees; do not overwrite newer framing.");
        result.RegisterObjectModification(owner);
        Undo.RecordObject(owner, "Clear menu sightline between authored push boulders");
        azimuth.floatValue = 0f;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
        EditorSceneManager.MarkSceneDirty(scene);
        result.Log("Only presentationAzimuth changed: {0} -> 0 degrees. Scene dirty, NOT saved. Capture Main before accepting; Undo restores baseline.", previous);
    }
}
