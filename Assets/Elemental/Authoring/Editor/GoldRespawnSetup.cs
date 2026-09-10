using System;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class GoldRespawnSetup
    {
        [MenuItem("Elemental/VFX/Install Gold Respawn")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install gold respawn outside Play Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice") throw new InvalidOperationException("Open the existing EarthCoreSlice scene before installing gold respawn.");
            var flow = Find<FrontendFlowController>(scene); var duel = flow != null ? flow.MatchController : null;
            var gravity = Find<GravityWorldBehaviour>(scene);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Elemental/Content/Shaders/GoldRespawnRing.shader");
            if (duel == null || flow == null || gravity == null || shader == null || duel.PlayerTransform == null || duel.BotTransform == null)
                throw new InvalidOperationException("Existing duel, both actors, frontend, gravity and GoldRespawnRing shader are required.");
            Undo.RecordObject(duel, "Bind gold respawn authority"); duel.ConfigureRespawnPresentation(gravity);
            var presenter = duel.GetComponent<GoldRespawnPresenter>() ?? Undo.AddComponent<GoldRespawnPresenter>(duel.gameObject);
            Undo.RecordObject(presenter, "Bind gold respawn presentation"); presenter.Configure(duel, flow, shader);
            EditorUtility.SetDirty(duel); EditorUtility.SetDirty(presenter); EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Gold respawn explicitly bound to both existing actors; scene left dirty for review/save. No terrain or material assets rebuilt.");
        }
        private static T Find<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var value in root.GetComponentsInChildren<T>(true))
            {
                if (found != null) throw new InvalidOperationException($"Expected one scene {typeof(T).Name}; resolve duplicate ownership before installation.");
                found = value;
            }
            return found;
        }
    }
}
