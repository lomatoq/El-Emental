using System;
using Elemental.Presentation.Animation;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Time;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Presentation.UI
{
    /// <summary>Cosmetic resources and their source-renderer/camera leases have one owner.</summary>
    [DefaultExecutionOrder(1000),DisallowMultipleComponent]
    public sealed class MatchPresentationStage:MonoBehaviour
    {
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Elemental.MatchPresentation.Stage");
        public const uint StageRenderingLayer=1u<<7;
        [SerializeField] private FrontendFlowController frontend;
        [SerializeField] private EarthMvpDuelController duel;
        [SerializeField] private EarthDuelHud hud;
        [SerializeField] private EarthSceneReadinessGate readiness;
        [SerializeField] private CinematicMenuCamera cameraOwner;
        [SerializeField] private UnityEngine.Camera outputCamera;
        [SerializeField] private HumanoidCharacterPresentation player,bot;
        [SerializeField] private Mesh[] stoneMeshes;
        [SerializeField] private Material stoneMaterial;
        private readonly Transform[] stones=new Transform[12];
        private readonly MeshRenderer[] stoneRenderers=new MeshRenderer[12];
        private readonly RespawnVisualProxy[] proxies={new RespawnVisualProxy(),new RespawnVisualProxy()};
        private readonly Joint[][] joints=new Joint[2][];
        private readonly Arm[][] arms=new Arm[2][];
        private struct Arm{public Transform upper,lower,hand;public Quaternion upperRest,lowerRest,handRest;public float side;}
        private readonly float[] cachedHeights=new float[2];
        private readonly Light[] lamps=new Light[2];
        private PlanetMotor playerMotor,botMotor;
        private bool ready,over,stageEntered;
        private double began;
        private Vector3 stageFeet,stageUp,stageFacing;
        private float stageHeight;
        public bool Ready=>ready;
        public bool ResultsActive=>over;
        public Transform SubjectVisualRoot=>ready?proxies[SubjectId==EarthDuelFighterId.Player?0:1].Root.transform:null;
        public uint ResultGeneration { get; private set; }
        public EarthDuelFighterId SubjectId { get; private set; }
        public EarthDuelFighterId WinnerId { get; private set; }
        public bool HasWinner { get; private set; }
        public MatchStageOutcome Outcome { get; private set; }
        public int ActiveStoneCount { get; private set; }
        public int ActiveLightCount=>(lamps[0]!=null&&lamps[0].enabled?1:0)+(lamps[1]!=null&&lamps[1].enabled?1:0);
        public string Failure { get; private set; }
        private struct Joint{public Transform source;public Quaternion baseline;public Vector3 pitchAxis,rollAxis;public int kind;}
        public void Configure(FrontendFlowController flow,EarthMvpDuelController match,EarthDuelHud combatHud,
            EarthSceneReadinessGate gate,CinematicMenuCamera cinematic,UnityEngine.Camera camera,
            HumanoidCharacterPresentation local,HumanoidCharacterPresentation rival,Mesh[] meshes,Material material)
        {frontend=flow;duel=match;hud=combatHud;readiness=gate;cameraOwner=cinematic;outputCamera=camera;player=local;bot=rival;stoneMeshes=meshes;stoneMaterial=material;}
        private void LateUpdate()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.StageLate);
            using(Marker.Auto())
            {
                if(Failure!=null)return;
                if(!ready)
                {
                    if(readiness!=null&&!readiness.IsReady)return;
                    try{Warm();}catch(Exception error){Failure=error.Message;CleanupStage();Debug.LogError("Match stage unavailable: "+Failure,this);return;}
                }
                bool results=frontend.State==FrontendState.Combat&&duel.IsRoundOver;
                if(results&&!over)BeginResult();
                if(!results&&over)CleanupStage();
                if(results)TickResult();
                else if(frontend.State==FrontendState.Main)TickMain();
                else HideStones();
            }
        }
        private void Warm()
        {
            if(frontend==null||duel==null||hud==null||cameraOwner==null||outputCamera==null||player==null||bot==null||stoneMaterial==null||stoneMeshes==null||stoneMeshes.Length==0)
                throw new InvalidOperationException("Bind the existing frontend/duel/HUD/camera/actors and baked stone library explicitly.");
            if(GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp||!urp.useRenderingLayers)
                throw new InvalidOperationException("Enable the reviewed URP rendering-layer capability before stage light isolation.");
            playerMotor=player.GetComponentInParent<PlanetMotor>();botMotor=bot.GetComponentInParent<PlanetMotor>();
            if(playerMotor==null||botMotor==null)throw new InvalidOperationException("Both stage subjects require their existing planet motors.");
            CacheActor(0,player,playerMotor);CacheActor(1,bot,botMotor);
            for(int i=0;i<stones.Length;i++)
            {
                Mesh mesh=stoneMeshes[i%stoneMeshes.Length];if(mesh==null)throw new InvalidOperationException("A baked stage stone mesh is missing.");
                var item=new GameObject("Cosmetic stage stone "+i);item.transform.SetParent(transform,false);
                item.AddComponent<MeshFilter>().sharedMesh=mesh;stoneRenderers[i]=item.AddComponent<MeshRenderer>();
                stoneRenderers[i].sharedMaterial=stoneMaterial;stoneRenderers[i].shadowCastingMode=ShadowCastingMode.Off;
                stones[i]=item.transform;
                float size=.065f+(i%4)*.024f;stones[i].localScale=Vector3.one*(size/Mathf.Max(.001f,mesh.bounds.size.magnitude));
                item.SetActive(false);
            }
            for(int i=0;i<2;i++)
            {
                var item=new GameObject(i==0?"Isolated stage key":"Isolated stage rim");item.transform.SetParent(transform,false);
                lamps[i]=item.AddComponent<Light>();lamps[i].type=LightType.Point;lamps[i].shadows=LightShadows.None;
                lamps[i].range=4;lamps[i].enabled=false;
                item.AddComponent<UniversalAdditionalLightData>().renderingLayers=(int)StageRenderingLayer;
            }
            ready=true;
        }
        private void CacheActor(int index,HumanoidCharacterPresentation actor,PlanetMotor motor)
        {
            Animator animator=actor.Animator;if(animator==null||!animator.isHuman)throw new InvalidOperationException("Stage requires the mapped production Humanoid.");
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if(!RespawnVisualProxy.IsBodyRenderer(renderer))continue;
                foreach(var material in renderer.sharedMaterials)
                if(material!=null&&material.shader.name!="Elemental/Graphics V5/Rumble Rock Lit"&&!material.shader.name.StartsWith("Universal Render Pipeline/"))
                    throw new InvalidOperationException("Stage rendering-layer support must be verified for "+material.shader.name);
            }
            Vector3 up=motor.LocalUp;
            Transform cachedHead=animator.GetBoneTransform(HumanBodyBones.Head);
            if(cachedHead==null)throw new InvalidOperationException("Stage requires the cached Humanoid head for framing.");
            cachedHeights[index]=Mathf.Max(.5f,Vector3.Dot(cachedHead.position-motor.SupportFeetPoint(up),up)+.20f);
            proxies[index].Capture(actor.transform,transform,motor.SupportFeetPoint(up),true);
            cachedHeights[index]=Mathf.Max(cachedHeights[index],MeasureVisibleHeight(actor.transform,motor.SupportFeetPoint(up),up)+.08f);
            arms[index]=new[]{CacheArm(animator,true),CacheArm(animator,false)};
            HumanBodyBones[] bones={HumanBodyBones.Head,HumanBodyBones.Chest};
            joints[index]=new Joint[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                Transform source=animator.GetBoneTransform(bones[i]);
                if(source==null)continue;
                joints[index][i]=new Joint{source=source,baseline=source.localRotation,kind=i,
                    pitchAxis=source.parent.InverseTransformDirection(actor.transform.right),rollAxis=source.parent.InverseTransformDirection(actor.transform.forward)};
            }
        }
        private static Arm CacheArm(Animator animator,bool left)
        {
            var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(upper==null||lower==null||hand==null)throw new InvalidOperationException("Stage requires complete mapped upper arm, forearm and wrist chains.");
            return new Arm{upper=upper,lower=lower,hand=hand,upperRest=upper.localRotation,lowerRest=lower.localRotation,handRest=hand.localRotation,side=left?-1:1};
        }
        private void PoseCompleteArms(int index,Vector3 facing)
        {
            var proxy=proxies[index];Vector3 right=Vector3.Cross(stageUp,facing).normalized;
            foreach(var arm in arms[index])
            {
                // Reconstruct the whole chain from cached baselines. Editing only
                // upper-arm roll retained a bent idle elbow and folded hands into the torso.
                proxy.SetLocalBoneRotation(arm.upper,arm.upperRest);proxy.SetLocalBoneRotation(arm.lower,arm.lowerRest);proxy.SetLocalBoneRotation(arm.hand,arm.handRest);
                float openness=Outcome==MatchStageOutcome.Victory?.36f:Outcome==MatchStageOutcome.Defeat?.22f:.28f;
                proxy.AimBoneToward(arm.upper,arm.lower,right*(arm.side*openness)-stageUp+facing*.10f);
                proxy.AimBoneToward(arm.lower,arm.hand,right*(arm.side*.12f)-stageUp+facing*(Outcome==MatchStageOutcome.Victory?.24f:.12f));
            }
        }
        public bool TryGetStageBonePosition(HumanBodyBones bone,out Vector3 position)
        {
            position=default;if(!ready||!over)return false;int index=SubjectId==EarthDuelFighterId.Player?0:1;
            var source=(index==0?player:bot).Animator.GetBoneTransform(bone);
            if(source==null||!proxies[index].TryGetBone(source,out Transform copy))return false;
            position=copy.position;return true;
        }
        private static float MeasureVisibleHeight(Transform actor,Vector3 feet,Vector3 up)
        {
            float height=0;Mesh baked=null;
            try
            {
                foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
                {
                    if(!RespawnVisualProxy.IsBodyRenderer(renderer))continue;Mesh mesh;
                    if(renderer is SkinnedMeshRenderer skin){baked??=new Mesh();skin.BakeMesh(baked);mesh=baked;}
                    else{var filter=renderer.GetComponent<MeshFilter>();mesh=filter!=null?filter.sharedMesh:null;}
                    if(mesh==null)continue;
                    foreach(var vertex in mesh.vertices)height=Mathf.Max(height,Vector3.Dot(renderer.transform.TransformPoint(vertex)-feet,up));
                }
            }
            finally{if(baked!=null)Destroy(baked);}
            return height;
        }
        private void BeginResult()
        {
            over=true;stageEntered=false;began=Time.unscaledTimeAsDouble;ResultGeneration++;
            SubjectId=hud.LocalFighter;Outcome=MatchStageTimeline.Outcome(SubjectId,duel.PlayerScore,duel.BotScore);
            HasWinner=duel.PlayerScore!=duel.BotScore;WinnerId=duel.PlayerScore>duel.BotScore?EarthDuelFighterId.Player:EarthDuelFighterId.Bot;
            PlanetMotor motor=SubjectId==EarthDuelFighterId.Player?playerMotor:botMotor;
            stageUp=motor.LocalUp.normalized;stageFeet=motor.SupportFeetPoint(stageUp);stageFacing=motor.FacingForward;
            // Frame the standing proxy, even when the decisive hit left its live actor ragdolled.
            stageHeight=cachedHeights[SubjectId==EarthDuelFighterId.Player?0:1];HideStones();
        }
        private void TickResult()
        {
            float age=(float)(Time.unscaledTimeAsDouble-began);bool reduced=frontend.Preferences.ReducedMotion;
            if(age<(reduced?0:.15f))return;
            int index=SubjectId==EarthDuelFighterId.Player?0:1;
            if(!stageEntered){cameraOwner.BeginResultsStage(transform,proxies[index].Root.transform,reduced);stageEntered=true;}
            float sway=reduced?0:Mathf.Sin(age*.65f)*1.5f;
            foreach(Joint joint in joints[index])
            {
                if(joint.source==null)continue;
                float pitch=joint.kind==0?(Outcome==MatchStageOutcome.Defeat?22:Outcome==MatchStageOutcome.Victory?-5:0):
                    joint.kind==1?(Outcome==MatchStageOutcome.Defeat?12:-3)+sway:0;
                float roll=joint.kind>=2?(joint.kind==2?1:-1)*(Outcome==MatchStageOutcome.Victory?28:Outcome==MatchStageOutcome.Defeat?-8:5):0;
                proxies[index].SetLocalBoneRotation(joint.source,Quaternion.AngleAxis(pitch,joint.pitchAxis)*Quaternion.AngleAxis(roll,joint.rollAxis)*joint.baseline);
            }
            float yaw=reduced?0:Mathf.Sin(age*.13f)*5;
            Vector3 facing=Quaternion.AngleAxis(yaw,stageUp)*stageFacing;
            float lift=reduced?.04f:.08f+.025f*Mathf.Sin(age*.9f);
            proxies[index].RenderStage(stageFeet,Quaternion.LookRotation(facing,stageUp),lift,stageUp,StageRenderingLayer|1);
            PoseCompleteArms(index,facing);
            cameraOwner.SetResultsStageFrame(stageFeet+stageUp*(stageHeight*.5f+lift),stageUp,
                Quaternion.AngleAxis(24,stageUp)*stageFacing,stageHeight,MatchStageTimeline.Camera(age,reduced),reduced);
            TickStones(stageFeet,stageUp,facing,age,12,true,reduced);
            Vector3 right=Vector3.Cross(stageUp,facing).normalized;
            lamps[0].transform.position=stageFeet+stageUp*1.65f+facing*1.15f-right*.6f;
            lamps[1].transform.position=stageFeet+stageUp*1.45f-facing*.7f+right*.8f;
            lamps[0].color=new Color(.78f,.88f,1);lamps[0].intensity=1.3f;
            lamps[1].color=Outcome==MatchStageOutcome.Victory?new Color(.24f,.9f,.55f):Outcome==MatchStageOutcome.Defeat?new Color(1,.22f,.15f):new Color(1,.78f,.4f);
            lamps[1].intensity=1.0f;lamps[0].enabled=lamps[1].enabled=true;
        }
        private void TickMain()
        {
            PlanetMotor motor=hud.LocalFighter==EarthDuelFighterId.Player?playerMotor:botMotor;
            TickStones(motor.SupportFeetPoint(motor.LocalUp),motor.LocalUp,motor.FacingForward,(float)Time.unscaledTimeAsDouble,8,false,frontend.Preferences.ReducedMotion);
        }
        private void TickStones(Vector3 feet,Vector3 up,Vector3 facing,float time,int count,bool results,bool reduced)
        {
            Vector3 right=Vector3.Cross(up,facing).normalized;ActiveStoneCount=0;
            for(int i=0;i<stones.Length;i++)
            {
                if(i>=count){stones[i].gameObject.SetActive(false);continue;}
                float phase=i*2.39996f+(reduced?0:time*(.09f+i*.003f));
                float side=Mathf.Cos(phase);float radius=.63f+(i%3)*.12f;
                float height=.24f+(i%5)*.23f+(reduced?0:.065f*Mathf.Sin(phase*1.3f+i));
                Vector3 position=feet+right*(side*radius)+facing*(Mathf.Sin(phase)*.32f)+up*height;
                Vector3 viewport=outputCamera.WorldToViewportPoint(position);
                bool visible=results||viewport.z>0&&viewport.x>.58f&&viewport.x<.98f&&viewport.y>.12f&&viewport.y<.82f;
                stones[i].gameObject.SetActive(visible);if(!visible)continue;
                stones[i].SetPositionAndRotation(position,Quaternion.LookRotation(facing,up)*Quaternion.Euler(i*31+(reduced?0:time*4),i*47,i*17));
                stoneRenderers[i].renderingLayerMask=results?StageRenderingLayer|1:1;ActiveStoneCount++;
            }
        }
        private void HideStones(){foreach(Transform stone in stones)if(stone!=null)stone.gameObject.SetActive(false);ActiveStoneCount=0;}
        private void CleanupStage()
        {
            over=false;stageEntered=false;foreach(var proxy in proxies)proxy.HideAndRestoreSource();
            foreach(var light in lamps)if(light!=null)light.enabled=false;HideStones();cameraOwner?.EndResultsStage();
        }
        private void OnDisable()=>CleanupStage();
        private void OnDestroy(){CleanupStage();foreach(var proxy in proxies)proxy.Dispose();}
    }
}
