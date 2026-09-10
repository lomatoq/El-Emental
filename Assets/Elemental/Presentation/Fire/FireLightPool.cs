using System;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Elemental.Presentation.Fire
{
    // Existing explicit shared owner; stream lighting expands only during cold prewarm.
    public sealed class FireLightPool : MonoBehaviour
    {
        public const int MaximumLights=6;
        [SerializeField,Range(0,MaximumLights)] private int capacity=2;
        private readonly Light[] lights=new Light[MaximumLights];
        private readonly Object[] owners=new Object[MaximumLights];
        private readonly int[] samples=new int[MaximumLights];
        public int ActiveLights {get{int n=0;for(int i=0;i<capacity;i++)if(lights[i]!=null&&lights[i].enabled)n++;return n;}}
        private void Awake()=>Prewarm();
        private void Prewarm()
        {
            capacity=Mathf.Clamp(capacity,0,MaximumLights);
            for(int i=0;i<capacity;i++)if(lights[i]==null)
            {
                var child=new GameObject("Fire light "+i);child.transform.SetParent(transform,false);
                var lamp=child.AddComponent<Light>();lamp.type=LightType.Point;lamp.shadows=LightShadows.None;
                lamp.color=new Color(1,.42f,.075f);lamp.enabled=false;lights[i]=lamp;
            }
        }
        public void ReserveStreamLighting(){capacity=MaximumLights;Prewarm();}
        public void Publish(Object owner,Vector3 position,float intensity,float range)
        {PublishSample(owner,0,position,intensity,range);ReleaseExtras(owner,1);}
        public void PublishFlow(Object owner,Vector3 first,Vector3 second,Vector3 third,int count,float intensity,float range)
        {
            if(capacity!=MaximumLights)throw new InvalidOperationException("Prewarm the existing Fire light pool before stream admission.");
            count=Mathf.Clamp(count,0,3);
            if(count>0)PublishSample(owner,0,first,intensity*.9f,range);
            if(count>1)PublishSample(owner,1,second,intensity*.75f,range);
            if(count>2)PublishSample(owner,2,third,intensity*.6f,range);
            ReleaseExtras(owner,count);
        }
        private void PublishSample(Object owner,int sample,Vector3 position,float intensity,float range)
        {
            if(owner==null)return;int available=-1;
            for(int i=0;i<capacity;i++)
            {
                if(owners[i]==owner&&samples[i]==sample){available=i;break;}
                if(owners[i]==null&&available<0)available=i;
            }
            if(available<0||lights[available]==null)return;
            owners[available]=owner;samples[available]=sample;
            var lamp=lights[available];lamp.transform.position=lamp.enabled?Vector3.Lerp(lamp.transform.position,position,1-Mathf.Exp(-Time.deltaTime*18)):position;lamp.intensity=Mathf.Max(0,intensity)*Elemental.Simulation.Fire.FireLightEnvelope.Sample(Time.time,available*1.71f);lamp.range=Mathf.Max(6f,range);lamp.enabled=intensity>0;
        }
        private void ReleaseExtras(Object owner,int retained)
        {for(int i=0;i<capacity;i++)if(owners[i]==owner&&samples[i]>=retained){owners[i]=null;if(lights[i]!=null)lights[i].enabled=false;}}
        public void Release(Object owner)=>ReleaseExtras(owner,0);
        private void OnDisable(){for(int i=0;i<capacity;i++){owners[i]=null;if(lights[i]!=null)lights[i].enabled=false;}}
    }
}
