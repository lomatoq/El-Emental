using Elemental.Simulation.Fire;
using UnityEngine;
namespace Elemental.Presentation.Fire
{
    [DefaultExecutionOrder(12000)]
    public sealed class FireAbilityLighting:MonoBehaviour
    {
        public const int Capacity=12;
        private static readonly Unity.Profiling.ProfilerMarker Marker=new("Elemental.Fire.Radiance");
        private readonly Light[] lamps=new Light[Capacity];
        private readonly Vector3[] points=new Vector3[8],ups=new Vector3[8];
        private readonly float[] heat=new float[8];
        private FireAbilityEffects effects;private FireSmolderPresentation burning;
        public int ActiveLights {get;private set;}
        public int ActiveBurnLights {get;private set;}
        public void Configure(FireAbilityEffects source,FireSmolderPresentation ignition)
        {
            effects=source;burning=ignition;
            for(int i=0;i<Capacity;i++)
            {var go=new GameObject("Fire ability radiance "+i);go.transform.SetParent(transform,false);var light=go.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.enabled=false;lamps[i]=light;}
        }
        private void LateUpdate()
        {
            using var marker=Marker.Auto();
            ActiveLights=ActiveBurnLights=0;if(effects==null||burning==null)return;
            int count=effects.CopyHotSamples(points,ups,heat);
            for(int i=0;i<8;i++)Apply(i,i<count, i<count?points[i]:default,i<count?ups[i]:default,i<count?heat[i]:0,7f,3.8f);
            count=burning.CopyIgnitionLights(points,ups,heat);ActiveBurnLights=count;
            for(int i=0;i<4;i++)Apply(8+i,i<count,i<count?points[i]:default,i<count?ups[i]:default,i<count?heat[i]:0,4.5f,.65f);
        }
        private void Apply(int index,bool active,Vector3 point,Vector3 up,float energy,float range,float intensity)
        {
            var lamp=lamps[index];if(lamp==null)return;bool continuing=lamp.enabled;lamp.enabled=active&&energy>.01f;if(!lamp.enabled)return;
            float pulse=FireLightEnvelope.Sample(Time.time,index*1.91f,energy);
            Vector3 target=point+up*.16f;lamp.transform.position=continuing?Vector3.Lerp(lamp.transform.position,target,1-Mathf.Exp(-Time.deltaTime*18)):target;lamp.range=range;
            lamp.color=Color.Lerp(new Color(1,.25f,.025f),new Color(1,.54f,.12f),Mathf.Clamp01(pulse*.65f));
            lamp.intensity=intensity*pulse;ActiveLights++;
        }
        private void OnDisable(){ActiveLights=ActiveBurnLights=0;foreach(var lamp in lamps)if(lamp!=null)lamp.enabled=false;}
    }
}
