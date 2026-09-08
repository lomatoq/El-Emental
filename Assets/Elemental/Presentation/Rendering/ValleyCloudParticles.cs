using UnityEngine;
using UnityEngine.Rendering;
using Elemental.Presentation.UI;
namespace Elemental.Presentation.Rendering
{
    // A single bounded native particle renderer; positions stay in the authored planet frame.
    [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(ParticleSystem))]
    public sealed class ValleyCloudParticles:MonoBehaviour
    {
        public const string OwnedName="Valley Image Cloud Particles";
        public const int ParticleCount=52;
        // Authored banks cover both actual viewing corridors, not four arbitrary quadrants.
        // xyz is the stationary valley-frame centre; w is the broad cloud width.
        private static readonly Vector4[] CloudBanks = {
            new Vector4(-800,110,1000,650),new Vector4(-300,180,1200,700),
            new Vector4(300,100,1050,600),new Vector4(900,230,1600,840),
            new Vector4(-1300,250,2200,1000),new Vector4(600,360,2500,1050),
            new Vector4(-150,40,700,320),new Vector4(1450,80,2100,1000),
            new Vector4(-300,110,-1300,650),new Vector4(180,210,-1700,850),
            new Vector4(700,150,-1900,900),new Vector4(1250,240,-2500,1100),
            new Vector4(-950,200,-2200,1100),new Vector4(380,30,-950,380),
            new Vector4(900,320,-2500,900),new Vector4(-50,330,-2600,950),
            // Fill the side views and separate small low wisps from high banks.
            new Vector4(-1100,25,350,280),new Vector4(1150,60,-350,360),
            new Vector4(-1700,130,-250,670),new Vector4(1850,170,250,760),
            new Vector4(-2100,410,650,1180),new Vector4(2300,480,-700,1320),
            new Vector4(-550,65,1600,410),new Vector4(550,55,-1450,300),
            new Vector4(-400,560,2900,1450),new Vector4(450,610,-3100,1550),
            new Vector4(-2400,300,-1250,940),new Vector4(2450,350,1350,1060)
        };
        private static int activeOwners;
        public static bool HasActive=>activeOwners>0;
        [SerializeField] private Material cloudMaterial;
        [SerializeField] private ValleyAtmosphereController atmosphere;
        [SerializeField] private FrontendFlowController frontend;
        private ParticleSystem particles;
        private bool motionPlaying;
        private void LateUpdate()
        {
            if(!Application.isPlaying || particles==null)return;
            SetMotion(atmosphere!=null && atmosphere.AnimateClouds && atmosphere.FogEnabled && atmosphere.CloudsEnabled &&
                (frontend==null || !frontend.Preferences.ReducedMotion));
        }
        private void SetMotion(bool active)
        {
            if(active==motionPlaying)return;motionPlaying=active;
            if(active)particles.Play(true);else particles.Pause(true);
        }
        private void OnEnable(){activeOwners++;if(cloudMaterial!=null)Populate();}
        private void OnDisable(){activeOwners=Mathf.Max(0,activeOwners-1);}
        public void Configure(ValleyAtmosphereController frame,Material material,FrontendFlowController flow=null)
        {
            if(frame==null || material==null)throw new System.ArgumentException("Explicit authored atmosphere frame and cloud material required.");
            transform.SetParent(frame.transform,false);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
            cloudMaterial=material;atmosphere=frame;frontend=flow;Populate();
        }
        private void Populate()
        {
            particles=GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleCount;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.useUnscaledTime=true;main.startLifetime=1000000;main.startSpeed=0;
            main.startSize3D=true;main.scalingMode=ParticleSystemScalingMode.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            particles.useAutoRandomSeed=false;particles.randomSeed=90317;
            var emission=particles.emission;emission.enabled=false;
            var shape=particles.shape;shape.enabled=false;
            var noise=particles.noise;noise.enabled=true;noise.separateAxes=true;noise.strengthX=12;noise.strengthY=3;noise.strengthZ=8;
            noise.frequency=0.0015f;noise.scrollSpeed=0.015f;noise.damping=false;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
            var collision=particles.collision;collision.enabled=false;
            var renderer=GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=cloudMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.alignment=ParticleSystemRenderSpace.View;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.flip=new Vector3(0.5f,0,0);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.minParticleSize=0;renderer.maxParticleSize=2;
            for(int i=0;i<ParticleCount;i++)
            {
                int part=i%4;Vector4 bank=CloudBanks[i%CloudBanks.Length];float width=bank.w;
                if(i>=CloudBanks.Length)
                {
                    bank.x+=Mathf.Sin(i*3.17f)*width*.35f;bank.z+=Mathf.Cos(i*2.71f)*width*.2f;
                    bank.y+=width*(.10f+(i%3)*.045f);width*=.28f+(i%4)*.09f;
                }
                var item=new ParticleSystem.EmitParams{position=new Vector3(bank.x,bank.y,bank.z),
                    startSize3D=new Vector3(width,width*(0.42f+part*0.045f),1),startLifetime=1000000,startColor=Color.white,
                    rotation=(part-1.5f)*4,randomSeed=(uint)(90317+i*431)};
                particles.Emit(item,1);
            }
            particles.Pause();motionPlaying=false;
            if(Application.isPlaying)SetMotion(atmosphere!=null && atmosphere.AnimateClouds && (frontend==null || !frontend.Preferences.ReducedMotion));
        }
    }
}
