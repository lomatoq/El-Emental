using System;
using Elemental.Presentation.Animation;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Fire
{
    /// <summary>Explicit local duel bridge. One prewarmed view per world slot survives hold release until retirement.</summary>
    [DefaultExecutionOrder(500),DisallowMultipleComponent]
    public sealed class FireStreamPresentationBinding:MonoBehaviour
    {
        [SerializeField] private Material smolderSmokeMaterial;
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Elemental.Fire.StreamPresentation");
        [SerializeField] private FireWorldBehaviour world;
        [SerializeField] private EarthMvpDuelController duel;
        [SerializeField] private FrontendFlowController frontend;
        [SerializeField] private HumanoidCharacterPresentation player,bot;
        [SerializeField] private FireStreamSession playerSession,botSession;
        [SerializeField] private FireVisualProfile visualProfile;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private FireLightPool lights;
        [SerializeField] private LayerMask collisionMask=~0;
        [SerializeField] private bool fluidDemonstration=true;
        [SerializeField] private EarthSurfaceScarPool fireScarPool;
        public FireAbilityController PlayerAbilities {get;private set;}
        public void ConfigureAbilityScarPool(EarthSurfaceScarPool pool)=>fireScarPool=pool;
        private FirePresentationController[] views;
        private FirePresentationSnapshot[] snapshots;
        private FireGroupHandle[] handles;
        private EarthCharacterPoseController playerPose,botPose;
        private Transform playerMuzzle,botMuzzle;
        private bool initialized;
        private bool cosmeticRenderingForQa=true;
        public bool CosmeticRenderingForQa=>cosmeticRenderingForQa;
        public void SetCosmeticRenderingForQa(bool enabled)
        {
            if(cosmeticRenderingForQa==enabled)return;
            cosmeticRenderingForQa=enabled;
            if(enabled)return;
            ActiveViews=DrainingViews=0;
            if(views!=null)for(int i=0;i<views.Length;i++)if(views[i]!=null){views[i].Retire();handles[i]=default;}
        }
        public bool IsReady=>initialized;
        public FireStreamSession PlayerSession=>playerSession;
        public FireStreamSession BotSession=>botSession;
        public FireWorldBehaviour World=>world;
        public Transform PlayerMuzzle=>playerMuzzle;
        public int PresenterCount=>views==null?0:views.Length;
        public int ActiveViews { get; private set; }
        public int DrainingViews { get; private set; }
        public int PoseBegins { get; private set; }
        public string Failure { get; private set; }
        public static bool SupportsLocalStream(FrontendState state,bool networkRound)=>state==FrontendState.Combat&&!networkRound;
        public FirePresentationController Presenter(int slot)=>views!=null&&slot>=0&&slot<views.Length?views[slot]:null;
        public void Configure(FireWorldBehaviour fireWorld,EarthMvpDuelController match,FrontendFlowController flow,
            HumanoidCharacterPresentation local,HumanoidCharacterPresentation rival,FireStreamSession localSession,
            FireStreamSession rivalSession,FireVisualProfile profile,UnityEngine.Camera camera,FireLightPool lightPool,int mask=~0)
        {
            if(initialized)throw new InvalidOperationException("Fire presentation is already initialized; replace it only after owner teardown.");
            world=fireWorld;duel=match;frontend=flow;player=local;bot=rival;playerSession=localSession;botSession=rivalSession;
            visualProfile=profile;viewCamera=camera;lights=lightPool;collisionMask=mask;
        }
        private void Update()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.FireBindingUpdate);
            if(!initialized)
            {
                if(Failure!=null)return;
                if(world==null||duel==null||frontend==null||player==null||bot==null||playerSession==null||botSession==null||visualProfile==null||viewCamera==null)
                {Fail("Bind world, duel, frontend, both production humanoids/sessions, CPU profile and camera explicitly.");return;}
                if(!world.IsReady)return;
                if(!player.PrepareFireChannelPresentation()||!bot.PrepareFireChannelPresentation())return;
                try{Initialize();}catch(Exception error){Fail(error.Message);return;}
            }
            bool local=SupportsLocalStream(frontend.State,frontend.IsNetworkRound);
            playerSession.SetLocalCapability(local);botSession.SetLocalCapability(local);
            UpdatePose(playerSession,playerPose);UpdatePose(botSession,botPose);
        }
        private void Initialize()
        {
            if(world.World.Capacity!=4&&world.World.Capacity!=8)throw new InvalidOperationException("Local Fire presentation requires a bounded four- or eight-slot world.");
            if(visualProfile.Backend!=FireVisualBackendSelection.CpuMesh||!visualProfile.CoherentBody||visualProfile.CpuMaterial==null||visualProfile.CoherentBodyShader==null)
                throw new InvalidOperationException("Local stream needs the explicit CPU coherent-body profile; native capability is not inferred.");
            if(fluidDemonstration&&lights!=null)lights.ReserveStreamLighting();
            playerPose=player.PoseController;botPose=bot.PoseController;
            if(playerPose==null||botPose==null)throw new InvalidOperationException("Production humanoid pose controllers must initialize before Fire binding.");
            playerMuzzle=CreateMuzzle(player);botMuzzle=CreateMuzzle(bot);
            playerSession.Configure(world,duel,EarthDuelFighterId.Player,playerMuzzle,collisionMask);
            botSession.Configure(world,duel,EarthDuelFighterId.Bot,botMuzzle,collisionMask);
            var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);
            var motor=duel.PlayerTransform.GetComponentInChildren<PlanetMotor>(true);
            if(input==null||input.EarthExecutor==null||motor==null||fireScarPool==null)
                throw new InvalidOperationException("Bind the production player input/motor and explicit shared fire scar pool before Fire abilities initialize.");
            var executor=input.EarthExecutor;
            var playerImpact=playerSession.gameObject.AddComponent<FireWorldImpact>();
            var botImpact=botSession.gameObject.AddComponent<FireWorldImpact>();
            playerImpact.Configure(duel,EarthDuelFighterId.Player,executor.FireDebrisPool,executor.FireFeedbackHub);
            botImpact.Configure(duel,EarthDuelFighterId.Bot,executor.FireDebrisPool,executor.FireFeedbackHub);
            playerSession.ConfigureWorldImpact(playerImpact);botSession.ConfigureWorldImpact(botImpact);
            fireScarPool.ConfigureFire(playerImpact);fireScarPool.ConfigureFire(botImpact);
            if(smolderSmokeMaterial==null)throw new InvalidOperationException("Bind smolder smoke material before Fire initialization.");
            var smolder=gameObject.AddComponent<FireSmolderPresentation>();
            smolder.Configure(playerImpact,botImpact,smolderSmokeMaterial);
            smolder.ConfigureIgnitionVisuals(visualProfile,viewCamera,collisionMask,world);
            PlayerAbilities=playerSession.gameObject.AddComponent<FireAbilityController>();
            PlayerAbilities.Configure(playerSession,duel,EarthDuelFighterId.Player,motor,playerMuzzle,
                player.Animator.GetBoneTransform(HumanBodyBones.RightFoot),playerImpact,collisionMask,player.Animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            player.ConfigureFireAbilities(PlayerAbilities);
            input.ConfigureFireAbilities(PlayerAbilities);
            var effects=gameObject.AddComponent<FireAbilityEffects>();
            effects.Configure(PlayerAbilities,player.Animator,playerPose,visualProfile,viewCamera,collisionMask);
            gameObject.AddComponent<FireAbilityLighting>().Configure(effects,smolder);
            views=new FirePresentationController[world.World.Capacity];snapshots=new FirePresentationSnapshot[views.Length];handles=new FireGroupHandle[views.Length];
            for(int slot=0;slot<views.Length;slot++)
            {
                var child=new GameObject("Local Fire presenter slot "+slot);child.transform.SetParent(transform,false);
                var view=child.AddComponent<FirePresentationController>();view.Configure(visualProfile,viewCamera,lights);
                snapshots[slot]=new FirePresentationSnapshot{Lifecycle=FireLifecycle.Retired};
                view.Publish(snapshots[slot]); // Allocate backend/buffers before the first hold, with no live group.
                if(!view.IsReady)throw new InvalidOperationException(view.BackendFailure??"CPU Fire presenter did not prewarm.");
                if(fluidDemonstration)view.CpuDiagnostics.EnableFluidDemonstration(Resources.Load<Shader>("FireFlowParcel"),collisionMask);
                views[slot]=view;
            }
            playerSession.Began+=PlayerBegan;playerSession.Ended+=PlayerEnded;
            botSession.Began+=BotBegan;botSession.Ended+=BotEnded;
            initialized=true;
        }
        private static Transform CreateMuzzle(HumanoidCharacterPresentation actor)
        {
            Animator animator=actor.Animator;
            if(animator==null||!animator.isHuman)throw new InvalidOperationException("Fire muzzle requires the actual production Humanoid Animator.");
            Transform hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            if(hand==null)throw new InvalidOperationException("Production Avatar has no RightHand mapping.");
            var child=new GameObject("Fire stream palm muzzle");child.transform.SetParent(hand,false);
            child.transform.localPosition=new Vector3(0,.015f,.045f);return child.transform;
        }
        private void PlayerBegan(uint generation,FireGroupHandle handle)
        { views[handle.Slot].CpuDiagnostics.FlowDiagnostics?.Collision.SetEmitter(duel.PlayerTransform);BeginPose(playerSession,player,generation); }
        private void BotBegan(uint generation,FireGroupHandle handle)
        { views[handle.Slot].CpuDiagnostics.FlowDiagnostics?.Collision.SetEmitter(duel.BotTransform);BeginPose(botSession,bot,generation); }
        private void BeginPose(FireStreamSession session,HumanoidCharacterPresentation actor,uint generation)
        {if(!actor.BeginFireChannelPresentation(generation,session.AimPoint)){session.Stop();return;}PoseBegins++;}
        private void PlayerEnded(uint generation)=>player?.EndFireChannelPresentation(generation);
        private void BotEnded(uint generation)=>bot?.EndFireChannelPresentation(generation);
        private static void UpdatePose(FireStreamSession session,EarthCharacterPoseController pose)
        {
            if(session.IsActive&&!pose.UpdateFireChannelPresentation(session.Generation,session.AimPoint))session.Stop();
        }
        private void LateUpdate()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.FireBindingLate);
            if(!initialized||!cosmeticRenderingForQa)return;
            using(Marker.Auto())
            {
                ActiveViews=DrainingViews=0;
                for(int slot=0;slot<views.Length;slot++)
                {
                    if(!world.World.TryGetHandle(slot,out FireGroupHandle current)||!world.CopySnapshot(current,snapshots[slot]))
                    {if(handles[slot].IsValid){views[slot].Retire();handles[slot]=default;}continue;}
                    if(handles[slot].IsValid&&handles[slot]!=current)views[slot].Retire();
                    handles[slot]=current;views[slot].Publish(snapshots[slot]);
                    if(snapshots[slot].Lifecycle==FireLifecycle.Draining)DrainingViews++;else ActiveViews++;
                }
            }
        }
        private void Fail(string reason)
        {Failure=reason;playerSession?.SetLocalCapability(false);botSession?.SetLocalCapability(false);Debug.LogError("Local Fire presentation unavailable: "+reason,this);}
        private void OnDisable()
        {
            ActiveViews=DrainingViews=0;
            playerSession?.SetLocalCapability(false);botSession?.SetLocalCapability(false);
            player?.CancelExternalFirePresentation();bot?.CancelExternalFirePresentation();
            if(views!=null)for(int i=0;i<views.Length;i++)if(views[i]!=null){views[i].Retire();handles[i]=default;}
        }
        private void OnDestroy()
        {
            if(playerSession!=null){playerSession.Began-=PlayerBegan;playerSession.Ended-=PlayerEnded;}
            if(botSession!=null){botSession.Began-=BotBegan;botSession.Ended-=BotEnded;}
            if(playerMuzzle!=null)Destroy(playerMuzzle.gameObject);if(botMuzzle!=null)Destroy(botMuzzle.gameObject);
        }
    }
}
