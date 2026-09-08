using UnityEngine;
using UnityEngine.Rendering;
namespace Elemental.Presentation.Rendering
{
    // A single bounded native particle renderer; positions stay in the authored planet frame.
    [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(ParticleSystem))]
    public sealed class ValleyCloudParticles:MonoBehaviour
    {
        public const string OwnedName="Valley Image Cloud Particles";
        public const int ParticleCount=16;
        private static int activeOwners;
        public static bool HasActive=>activeOwners>0;
        [SerializeField] private Material cloudMaterial;
        private ParticleSystem particles;
        private void OnEnable(){activeOwners++;if(cloudMaterial!=null)Populate();}
        private void OnDisable(){activeOwners=Mathf.Max(0,activeOwners-1);}
        public void Configure(ValleyAtmosphereController frame,Material material)
        {
            if(frame==null || material==null)throw new System.ArgumentException("Explicit authored atmosphere frame and cloud material required.");
            transform.SetParent(frame.transform,false);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
            cloudMaterial=material;Populate();
        }
        private void Populate()
        {
            particles=GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleCount;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.useUnscaledTime=false;main.startLifetime=1000000;main.startSpeed=0;
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
                // Four uneven banks around both camera directions, with clear air between.
                int bank=i/4,part=i%4;float angle=(bank*90+25+part*9)*Mathf.Deg2Rad;
                float distance=800+bank*130+part*180;float width=580+part*115+(bank%2)*90;
                var item=new ParticleSystem.EmitParams{position=new Vector3(Mathf.Sin(angle)*distance,-35+part*38+(bank%2)*30,Mathf.Cos(angle)*distance),
                    startSize3D=new Vector3(width,width*(0.42f+part*0.045f),1),startLifetime=1000000,startColor=Color.white,
                    rotation=(part-1.5f)*4,randomSeed=(uint)(90317+i*431)};
                particles.Emit(item,1);
            }
            if(Application.isPlaying)particles.Play();else particles.Pause();
        }
    }
}
