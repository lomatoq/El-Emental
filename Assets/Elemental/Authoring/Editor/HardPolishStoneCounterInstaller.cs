using System;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishStoneCounterInstaller
    {
        [MenuItem("Elemental/QA/Hard Polish/Bind Stone Counter Guard")]
        public static void InstallActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity")
                throw new InvalidOperationException("Open the existing EarthCoreSlice scene before installing counter guard.");
            Install(scene); EditorSceneManager.SaveScene(scene);
        }
        public static EarthStoneCounterGuard Install(Scene scene)
        {
            var duel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<FrontendFlowController>(true)).Single().MatchController;
            if (duel == null || duel.PlayerTransform == null)
                throw new InvalidOperationException("The frontend must reference the saved local duel and player.");
            var input = duel.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single();
            var pose = duel.PlayerTransform.GetComponentsInChildren<EarthCharacterPoseController>(true).Single();
            var body = input.GetComponent<Rigidbody>();
            var pool = input.EarthExecutor != null ? input.EarthExecutor.FragmentPool?.DebrisPool : null;
            if (body == null || pool == null) throw new InvalidOperationException("Bind the existing player Rigidbody and fragment/debris pool first.");
            var guard = input.GetComponent<EarthStoneCounterGuard>() ?? Undo.AddComponent<EarthStoneCounterGuard>(input.gameObject);
            guard.Configure(body, input.GetComponent<PlanetMotor>(), pool, input.GetComponent<ActiveRagdollPuppet>());
            var presenter = input.GetComponent<EarthStoneCounterPresenter>() ?? Undo.AddComponent<EarthStoneCounterPresenter>(input.gameObject);
            presenter.Configure(guard, pose);
            EditorUtility.SetDirty(guard); EditorUtility.SetDirty(presenter); EditorSceneManager.MarkSceneDirty(scene);
            return guard;
        }
    }
}
