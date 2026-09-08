using System;
using Elemental.Input.Gestures;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Unity.Cinemachine;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Explicit local presentation ownership; gameplay/network outcome authority stays in the binding.</summary>
    [DefaultExecutionOrder(-4500), DisallowMultipleComponent]
    public sealed class EarthOnlinePresentationBridge : MonoBehaviour
    {
        [Serializable]
        public sealed class View
        {
            public Camera Output;
            public AudioListener Listener;
            public CinemachineBrain Brain;
            public CinemachineCamera GameplayCamera;
            public EarthCinemachineCameraController Controller;
            public PlanetCameraRig Rig;
            public EarthCameraDirector Director;
            public EarthChargeCameraLookdevV2 Charge;
            public EarthCinematicDepthOfFieldController DepthOfField;
            public EarthAnimationDriver Animation;
            public PlanetMotor Motor;
            public Transform Subject;
            public MagicInputController Magic;
            public MagicExecutor Executor;
            public EarthDualMouseAbilityController DualMouse;
            public EarthPillarMobility Pillar;
            public EarthPillarWaveAbility Wave;
            public Behaviour[] AdditionalCameraDrivers = Array.Empty<Behaviour>();
            [NonSerialized] internal Transform OriginalDofPrimary, OriginalDofSecondary;
            [NonSerialized] internal Behaviour[] Owned;
            [NonSerialized] internal bool[] AuthoredEnabled;

            internal void Capture()
            {
                if (Output == null || Listener == null || Brain == null || GameplayCamera == null ||
                    Controller == null || Rig == null || Director == null || Animation == null || Motor == null ||
                    Subject == null || Magic == null || Executor == null || DualMouse == null)
                    throw new InvalidOperationException("Online camera view needs the complete explicitly authored actor/camera graph.");
                if (Listener.gameObject != Output.gameObject || Brain.gameObject != Output.gameObject)
                    throw new InvalidOperationException("Output camera, listener and Cinemachine brain must share their authored owner.");
                var owned = new System.Collections.Generic.List<Behaviour>
                    { Controller, Rig, Director, GameplayCamera, Brain, Charge, DepthOfField };
                if (AdditionalCameraDrivers != null) owned.AddRange(AdditionalCameraDrivers);
                owned.RemoveAll(value => value == null);
                if (owned.Contains(Output) || owned.Contains(Listener))
                    throw new InvalidOperationException("Additional camera drivers must not repeat output/listener ownership.");
                Owned = owned.ToArray(); AuthoredEnabled = new bool[Owned.Length];
                if (DepthOfField != null) { OriginalDofPrimary = DepthOfField.PrimarySubject; OriginalDofSecondary = DepthOfField.SecondarySubject; }
                for (int i = 0; i < Owned.Length; i++) AuthoredEnabled[i] = Owned[i].enabled;
            }
            internal void Enable(bool selected)
            {
                // Controller.OnEnable has a legacy listener side effect. Apply all
                // controllers first; the bridge sets final output/listener states last.
                for (int i = 0; i < Owned.Length; i++)
                    if (Owned[i] != null) Owned[i].enabled = selected && AuthoredEnabled[i];
            }
        }

        [SerializeField] private EarthOnlineGameplayBinding binding;
        [SerializeField] private FrontendFlowController flow;
        [SerializeField] private CinematicMenuCamera menu;
        [SerializeField] private CinemachineCamera menuVirtualCamera;
        [SerializeField] private EarthDuelHud hud;
        [SerializeField] private EarthSceneReadinessGate readiness;
        [SerializeField] private Transform planet, arena;
        [SerializeField] private View actorOne, actorTwo;
        private bool _captured, _subscribed, _online;
        public byte LocalPerspective { get; private set; } = 1;
        public Camera ActiveOutput => LocalPerspective == 2 ? actorTwo.Output : actorOne.Output;

        public void Configure(EarthOnlineGameplayBinding world, FrontendFlowController frontend,
            CinematicMenuCamera menuOwner, CinemachineCamera menuCamera, EarthDuelHud combatHud,
            EarthSceneReadinessGate gate, Transform planetCenter, Transform arenaMarker, View one, View two)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bind online presentation explicitly before Play mode.");
            binding = world; flow = frontend; menu = menuOwner; menuVirtualCamera = menuCamera;
            hud = combatHud; readiness = gate; planet = planetCenter; arena = arenaMarker; actorOne = one; actorTwo = two;
        }
        private void Awake() => Capture();
        private void Capture()
        {
            if (_captured) return;
            if (binding == null || flow == null || menu == null || menuVirtualCamera == null || hud == null ||
                readiness == null || planet == null || arena == null || actorOne == null || actorTwo == null)
                throw new InvalidOperationException("Online presentation needs explicit scene UI, arena and both camera views.");
            actorOne.Capture(); actorTwo.Capture();
            if (actorOne.Output == actorTwo.Output || actorOne.Listener == actorTwo.Listener)
                throw new InvalidOperationException("Online actors must have distinct cloned camera owners.");
            ValidateOwnership(actorOne, binding.ActorOne); ValidateOwnership(actorTwo, binding.ActorTwo);
            _captured = true;
        }
        private static void ValidateOwnership(View view, EarthOnlineGameplayBinding.Actor actor)
        {
            if (actor == null || actor.Motor != view.Motor || actor.Executor != view.Executor ||
                actor.MagicInput != view.Magic || actor.CameraFrame != view.Rig.transform)
                throw new InvalidOperationException("Camera/HUD view does not match its authored online actor references.");
            foreach (Behaviour control in actor.LocalOnlyControls)
                if (control == view.Output || control == view.Listener || Array.IndexOf(view.Owned, control) >= 0)
                    throw new InvalidOperationException("Camera controls belong exclusively to EarthOnlinePresentationBridge, not LocalOnlyControls.");
        }
        private void OnEnable()
        {
            if (binding == null || _subscribed) return;
            binding.Prepared += Prepared; binding.Ended += Ended; _subscribed = true;
        }
        private void Prepared(byte localActor)
        {
            Capture();
            if (localActor != 1 && localActor != 2) throw new ArgumentOutOfRangeException(nameof(localActor));
            _online = true;
            Select(localActor, binding.OnlineDuel, localActor == 1);
        }
        private void Select(byte actor, EarthMvpDuelController duel, bool restartAllowed)
        {
            // Finish first: restores the previous subject's .45 animation clock,
            // charge/DOF state and foreground visibility before rebinding to actor 2.
            bool wasMenu = menu.OwnsPresentation;
            menu.FinishCombatTransition();
            View view = actor == 2 ? actorTwo : actorOne;
            if (_online) view.DepthOfField?.ConfigureSubjects(view.Subject, actor == 2 ? actorOne.Subject : actorTwo.Subject);
            else
            {
                actorOne.DepthOfField?.ConfigureSubjects(actorOne.OriginalDofPrimary, actorOne.OriginalDofSecondary);
                actorTwo.DepthOfField?.ConfigureSubjects(actorTwo.OriginalDofPrimary, actorTwo.OriginalDofSecondary);
            }
            actorOne.Enable(false); actorTwo.Enable(false); view.Enable(true);
            actorOne.Output.enabled = actor == 1; actorTwo.Output.enabled = actor == 2;
            actorOne.Listener.enabled = actor == 1; actorTwo.Listener.enabled = actor == 2;
            LocalPerspective = actor;
            menu.Configure(menuVirtualCamera, view.Brain, view.Output, view.Charge, view.DepthOfField,
                view.Animation, view.Motor, view.Subject, _online ? (actor == 2 ? actorOne.Subject : actorTwo.Subject) : actorOne.OriginalDofSecondary, view.GameplayCamera, view.Controller);
            flow.SetPresentationCameraDirector(view.Director);
            hud.Configure(duel, readiness, view.Magic, view.Executor, view.DualMouse, view.Subject,
                planet, arena, view.Pillar, view.Wave);
            hud.SetLocalPerspective(actor == 1 ? EarthDuelFighterId.Player : EarthDuelFighterId.Bot, restartAllowed);
            if (wasMenu || flow.State != FrontendState.Combat)
                menu.Enter(flow.Preferences.ReducedMotion, flow.PresentationTransitionSeconds);
            // FrontendFlowController.BeginOnlineMatch remains the sole .85s
            // menu-to-combat transition owner; never directly force Combat here.
        }
        private void Ended()
        {
            if (!_online) return;
            _online = false; Select(1, binding.OfflineDuel, true);
        }
        private void OnDisable()
        {
            if (_subscribed && binding != null) { binding.Prepared -= Prepared; binding.Ended -= Ended; }
            _subscribed = false;
            if (_online && binding != null && menu != null && hud != null && flow != null &&
                actorOne != null && actorOne.Output != null && actorTwo != null && actorTwo.Output != null) Ended();
        }
    }
}



