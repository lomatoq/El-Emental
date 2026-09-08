using System;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Input.Gestures;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Elemental.Online.Editor
{
    /// <summary>Called by the explicit online scene composition installer; never discovers or replaces user actors.</summary>
    public static class OnlinePresentationAuthoring
    {
        public static EarthOnlinePresentationBridge Install(GameObject onlineOwner,
            EarthOnlineGameplayBinding binding, OnlineActorGraphAuthoring.Result actorTwoGraph,
            FrontendFlowController flow, CinematicMenuCamera menu, EarthDuelHud hud,
            EarthCinemachineCameraController authoredController, Behaviour[] additionalCameraDrivers)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || onlineOwner == null || binding == null ||
                actorTwoGraph == null || flow == null || menu == null || hud == null || authoredController == null)
                throw new InvalidOperationException("Provide the explicit online owner, actor graph clone and existing frontend/HUD/camera components in Edit mode.");
            if (actorTwoGraph.Container.activeSelf)
                throw new InvalidOperationException("The cloned online actor graph must remain inactive in the saved offline scene.");
            var one = new EarthOnlinePresentationBridge.View
            {
                Output = Read<Camera>(menu, "outputCamera"), Brain = Read<CinemachineBrain>(menu, "brain"),
                GameplayCamera = authoredController.VirtualCamera, Controller = authoredController,
                Rig = Read<PlanetCameraRig>(authoredController, "legacyRig"),
                Director = Read<EarthCameraDirector>(flow, "cameraDirector"),
                Charge = Read<EarthChargeCameraLookdevV2>(menu, "chargeLook", false),
                DepthOfField = Read<EarthCinematicDepthOfFieldController>(menu, "depthOfField", false),
                Animation = Read<EarthAnimationDriver>(menu, "animationDriver"),
                Motor = Read<PlanetMotor>(menu, "motor"), Subject = Read<Transform>(menu, "actor"),
                Magic = Read<MagicInputController>(hud, "magic"), Executor = Read<MagicExecutor>(hud, "executor"),
                DualMouse = Read<EarthDualMouseAbilityController>(hud, "dualMouse"),
                Pillar = Read<EarthPillarMobility>(hud, "pillar", false),
                Wave = Read<EarthPillarWaveAbility>(hud, "wave", false),
                AdditionalCameraDrivers = additionalCameraDrivers ?? Array.Empty<Behaviour>()
            };
            one.Listener = one.Output.GetComponent<AudioListener>();
            if (one.Listener == null) throw new InvalidOperationException("The existing output camera needs its authored listener; do not create a second listener.");
            var two = new EarthOnlinePresentationBridge.View
            {
                Output = Clone(actorTwoGraph, one.Output), Listener = Clone(actorTwoGraph, one.Listener),
                Brain = Clone(actorTwoGraph, one.Brain), GameplayCamera = Clone(actorTwoGraph, one.GameplayCamera),
                Controller = Clone(actorTwoGraph, one.Controller), Rig = Clone(actorTwoGraph, one.Rig),
                Director = Clone(actorTwoGraph, one.Director), Charge = Clone(actorTwoGraph, one.Charge),
                DepthOfField = Clone(actorTwoGraph, one.DepthOfField), Animation = Clone(actorTwoGraph, one.Animation),
                Motor = Clone(actorTwoGraph, one.Motor), Subject = Clone(actorTwoGraph, one.Subject),
                Magic = Clone(actorTwoGraph, one.Magic), Executor = Clone(actorTwoGraph, one.Executor),
                DualMouse = Clone(actorTwoGraph, one.DualMouse), Pillar = Clone(actorTwoGraph, one.Pillar),
                Wave = Clone(actorTwoGraph, one.Wave),
                AdditionalCameraDrivers = new Behaviour[one.AdditionalCameraDrivers.Length]
            };
            for (int i = 0; i < two.AdditionalCameraDrivers.Length; i++)
                two.AdditionalCameraDrivers[i] = Clone(actorTwoGraph, one.AdditionalCameraDrivers[i]);
            ValidateLocalControls(binding.ActorOne, one); ValidateLocalControls(binding.ActorTwo, two);
            if (Read<Transform>(hud, "player") != one.Subject || Read<EarthMvpDuelController>(hud, "duel") != binding.OfflineDuel)
                throw new InvalidOperationException("The offline HUD must reference actor one and the explicit offline duel; preserve its current user references.");
            foreach (GameObject root in onlineOwner.scene.GetRootGameObjects())
            {
                foreach (Camera output in root.GetComponentsInChildren<Camera>(true))
                    if (output != one.Output && output != two.Output && output.isActiveAndEnabled && output.targetTexture == null)
                        throw new InvalidOperationException("Another active output camera exists: " + output.name);
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    if (listener != one.Listener && listener != two.Listener && listener.isActiveAndEnabled)
                        throw new InvalidOperationException("Another active audio listener exists: " + listener.name);
            }
            var bridge = onlineOwner.GetComponent<EarthOnlinePresentationBridge>() ?? Undo.AddComponent<EarthOnlinePresentationBridge>(onlineOwner);
            bridge.Configure(binding, flow, menu, Read<CinemachineCamera>(menu, "menuCamera"), hud,
                Read<EarthSceneReadinessGate>(hud, "readiness"), Read<Transform>(hud, "planet"),
                Read<Transform>(hud, "arena"), one, two);
            EditorUtility.SetDirty(bridge);
            return bridge;
        }
        private static void ValidateLocalControls(EarthOnlineGameplayBinding.Actor actor, EarthOnlinePresentationBridge.View view)
        {
            if (actor == null || actor.Motor != view.Motor || actor.Executor != view.Executor)
                throw new InvalidOperationException("Complete actor gameplay bindings before installing presentation.");
            foreach (Behaviour control in actor.LocalOnlyControls)
                if (control == view.Output || control == view.Listener || control == view.Brain || control == view.GameplayCamera ||
                    control == view.Controller || control == view.Rig || control == view.Director || control == view.Charge ||
                    control == view.DepthOfField || Array.IndexOf(view.AdditionalCameraDrivers, control) >= 0)
                    throw new InvalidOperationException("Remove camera-only components from actor.LocalOnlyControls; the presentation bridge owns them.");
        }
        private static T Read<T>(UnityEngine.Object owner, string field, bool required = true) where T : UnityEngine.Object
        {
            var property = new SerializedObject(owner).FindProperty(field);
            if (property == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " is missing; update the explicit authoring seam.");
            T reference = property.objectReferenceValue as T;
            if (required && reference == null) throw new InvalidOperationException(owner.GetType().Name + "." + field + " requires its existing authored reference.");
            return reference;
        }
        private static T Clone<T>(OnlineActorGraphAuthoring.Result graph, T source) where T : UnityEngine.Object
        {
            if (source == null) return null;
            T target = graph.CloneOf(source);
            if (target == null || target == source)
                throw new InvalidOperationException("Include the complete owner graph for " + source.name + " (" + typeof(T).Name + "). It cannot remain bound to actor one.");
            return target;
        }
    }
}
