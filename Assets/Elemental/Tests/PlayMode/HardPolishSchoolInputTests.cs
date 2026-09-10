using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishSchoolInputTests
    {
        private Scene scene, previous;
        private Keyboard keyboard;
        private Mouse mouse;
        private FrontendFlowController flow;
        private EarthMvpDuelController duel;
        private MagicInputController magic;
        private FireStreamPresentationBinding binding;
        private float previousScale,previousCaptureStep;
        private InputSettings.BackgroundBehavior previousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private string InputStateEvidence => $"selected={magic.SelectedElement} flow={flow.State} player={duel.PlayerPhase} bot={duel.BotPhase} available={binding.PlayerSession.IsAvailable} context={magic.SchoolInputContextAvailable} stun={duel.PlayerTransform.GetComponent<PlanetMotor>().IsImpactStunned} paired={magic.GetComponent<PlayerInput>().devices.Count}";
        private System.IDisposable fireInputRival;
        private sealed class InputRivalIsolation:System.IDisposable
        {
            private readonly PlanetMotor motor;private readonly Rigidbody body;
            private readonly Vector3 position,velocity,angular;private readonly Quaternion rotation;private readonly bool enabled,kinematic;
            public InputRivalIsolation(Transform rival,PlanetMotor player)
            {
                motor=rival.GetComponent<PlanetMotor>();body=motor.Body;position=body.position;rotation=body.rotation;
                velocity=body.linearVelocity;angular=body.angularVelocity;enabled=motor.enabled;kinematic=body.isKinematic;
                motor.enabled=false;if(!kinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;
                body.position=player.Body.position+player.LocalUp*20+Vector3.Cross(player.LocalUp,player.FacingForward).normalized*20;Physics.SyncTransforms();
            }
            public void Dispose(){if(body==null)return;body.position=position;body.rotation=rotation;body.isKinematic=kinematic;if(!kinematic){body.linearVelocity=velocity;body.angularVelocity=angular;}motor.enabled=enabled;Physics.SyncTransforms();}
        }
        private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        [UnitySetUp] public IEnumerator Load()
        {
            previous = SceneManager.GetActiveScene(); previousScale = Time.timeScale;
            previousCaptureStep=Time.captureDeltaTime;Time.captureDeltaTime=1f/60;
            previousBackground=InputSystem.settings.backgroundBehavior;previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(scene);
            flow = All<FrontendFlowController>().Single(); duel = flow.MatchController;
            binding = All<FireStreamPresentationBinding>().Single();
            magic = duel.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single();
            var gate = All<EarthSceneReadinessGate>().Single();
            double end = Time.realtimeSinceStartupAsDouble + 140;
            while ((!gate.IsReady || !binding.IsReady || flow.State == FrontendState.Loading) && !gate.Failed && binding.Failure == null && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status); Assert.That(binding.IsReady, Is.True, binding.Failure);
            Assert.That(magic.FireStream, Is.SameAs(binding.PlayerSession), "Install the explicit production input binding first.");
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            magic.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            Assert.That(flow.BeginBot(), Is.True);
            while (flow.State != FrontendState.Combat && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); yield return null;
            var bot = duel.BotTransform.GetComponent<EarthMvpBotController>(); if (bot != null) bot.enabled = false;
            var motor=duel.PlayerTransform.GetComponent<PlanetMotor>();fireInputRival=new InputRivalIsolation(duel.BotTransform,motor);
            while((!binding.PlayerSession.IsAvailable||motor.IsImpactStunned||!magic.SchoolInputContextAvailable)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(binding.PlayerSession.IsAvailable && !motor.IsImpactStunned && magic.SchoolInputContextAvailable,Is.True,InputStateEvidence);
        }

        [UnityTearDown] public IEnumerator Unload()
        {
            fireInputRival?.Dispose();fireInputRival=null;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = previousScale;Time.captureDeltaTime=previousCaptureStep;
            InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
        }
        private IEnumerator KeyTap(Key key)
        {
            var held=keyboard.allKeys.Where(k=>k.isPressed&&k.keyCode!=key).Select(k=>k.keyCode).ToArray();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.Concat(new[]{key}).ToArray())); yield return null;yield return new WaitForEndOfFrame();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); yield return null;yield return new WaitForEndOfFrame();
        }
        private void Primary(bool held)
        {
            var state = new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .4f) };
            if(magic.SelectedElement==ElementId.Fire)InputSystem.QueueStateEvent(keyboard,held?new KeyboardState(Key.LeftShift):new KeyboardState());
            InputSystem.QueueStateEvent(mouse, held ? state.WithButton(magic.SelectedElement==ElementId.Fire?MouseButton.Middle:MouseButton.Left) : state);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RealSchoolKeysRouteFireBeforeEarthChordAndSuppressHeldAcrossSwitchPauseFocus()
        {
            var earth = magic.EarthExecutor;
            int changes = 0; magic.SelectedElementChanged += _ => changes++;
            yield return KeyTap(Key.Digit1);
            Assert.That(magic.SelectedElement, Is.EqualTo(ElementId.Fire)); Assert.That(changes, Is.EqualTo(1));
            int begins = binding.PoseBegins;
            Primary(true); yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.True, "Fire must begin on the first input frame, before Earth's chord delay.");
            uint generation = binding.PlayerSession.Generation;
            for (int i = 0; i < 8; i++) { yield return null; Assert.That(binding.PlayerSession.Generation, Is.EqualTo(generation)); }
            Assert.That(binding.PoseBegins, Is.EqualTo(begins + 1));
            Assert.That(magic.CurrentBendPhase, Is.EqualTo(BendPhase.Idle).Or.EqualTo(BendPhase.Cancelled),
                "Fire must leave Earth's gesture inactive; switching schools may retain its terminal Cancelled state.");
            yield return KeyTap(Key.Digit3); yield return KeyTap(Key.Digit4);
            Assert.That(magic.SelectedElement, Is.EqualTo(ElementId.Fire)); Assert.That(changes, Is.EqualTo(1));
            Assert.That(binding.PlayerSession.IsActive, Is.True, "Unavailable schools must not cancel the selected school.");
            var hud = All<EarthDuelHud>().Single().GetComponent<UIDocument>().rootVisualElement;
            Assert.That(hud.Q<Label>("element-action-hint"), Is.Null);
            Assert.That(hud.Q("reference-token-Water").resolvedStyle.opacity, Is.LessThan(.4f));
            yield return KeyTap(Key.Digit2);
            Assert.That(binding.PlayerSession.IsActive, Is.False); Assert.That(magic.SelectedElement, Is.EqualTo(ElementId.Earth));
            Assert.That(magic.EarthExecutor, Is.SameAs(earth)); Assert.That(magic.SchoolPrimarySuppressed, Is.True);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(magic.CurrentBendPhase, Is.EqualTo(BendPhase.Idle).Or.EqualTo(BendPhase.Cancelled)); Assert.That(earth.HeldBody, Is.Null);
            Primary(false); yield return null; yield return KeyTap(Key.Digit1);
            Primary(true); yield return null; Assert.That(binding.PlayerSession.IsActive, Is.True);
            flow.Pause(); yield return null; Assert.That(binding.PlayerSession.IsActive, Is.False);
            flow.Resume(); for (int i = 0; i < 4; i++) yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.False, "A held mouse cannot resume after pause.");
            Primary(false); yield return null; Primary(true); yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.True);
            magic.SendMessage("OnApplicationFocus", false); yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.False);
            magic.SendMessage("OnApplicationFocus", true); for (int i = 0; i < 4; i++) yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.False, "Focus return needs a fresh press.");
            // Higher-priority pose rejects admission; releasing that priority is not a new mouse edge.
            var actor = duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            Primary(false); yield return null;
            actor.PoseController.SetPresentationSuppressed(true);
            Primary(true); yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.False);
            actor.PoseController.SetPresentationSuppressed(false);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.False, "Rejected admission must not retry the held press.");
            Primary(false); yield return null; Primary(true); yield return null;
            Assert.That(binding.PlayerSession.IsActive, Is.True);
            Directory.CreateDirectory("BuildReports/SchoolInput");
            yield return new WaitForSecondsRealtime(2.6f); yield return new WaitForEndOfFrame();
            Assert.That(hud.Q<Label>("element-action-hint"), Is.Null);
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("BuildReports/SchoolInput/Fire-Hold-Hud.png", capture.EncodeToPNG()); Object.Destroy(capture);
            Primary(false); yield return null; Assert.That(binding.PlayerSession.IsActive, Is.False);
        }
    }
}
