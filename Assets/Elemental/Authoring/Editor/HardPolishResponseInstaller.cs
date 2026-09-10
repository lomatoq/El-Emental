using System;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Fire;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishResponseInstaller
    {
        [MenuItem("Elemental/VFX/Bind Polish Responses")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bind responses outside Play mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "EarthCoreSlice") throw new InvalidOperationException("Open the existing EarthCoreSlice.");
            T One<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
            var duel = One<FrontendFlowController>().MatchController; var fire = One<FireStreamPresentationBinding>();
            var hub = One<EarthMaterialFeedbackHub>(); var feedback = One<EarthMagicFeedback>();
            var flow = One<FrontendFlowController>();
            var local = duel.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single();
            var audio = local.EarthExecutor != null ? local.EarthExecutor.GetComponent<EarthAudioDirector>() : null;
            if (audio == null) throw new InvalidOperationException("The existing local Earth executor must own its audio director.");
            var bot = duel.BotTransform.GetComponentsInChildren<MagicInputController>(true).SingleOrDefault();
            var localRig = duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var botRig = duel.BotTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            Transform hand = localRig.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform botHand = botRig.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null || botHand == null || fire.PlayerSession == null || fire.BotSession == null)
                throw new InvalidOperationException("Bind both existing humanoid hands and Fire sessions first.");
            Undo.RecordObjects(new UnityEngine.Object[] { feedback, audio }, "Bind polish responses");
            feedback.ConfigureMaterialFeedback(hub);
            feedback.ConfigurePolishResponses(local, bot, fire.PlayerSession, fire.BotSession, hand, botHand, flow);
            audio.ConfigureResponseAudio(hub, fire.PlayerSession, fire.BotSession);
            EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(audio); EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
