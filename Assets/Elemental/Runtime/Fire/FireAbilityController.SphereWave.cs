using System;
using Elemental.Simulation.Fire;
using UnityEngine;
using Unity.Profiling;
namespace Elemental.Runtime.Fire
{
    public sealed partial class FireAbilityController
    {
        private static readonly ProfilerMarker SphereWaveMarker=new("Elemental.Fire.SphereWave.Fixed");
        private readonly Collider[] waveOverlap=new Collider[2048],waveTouched=new Collider[FireSphereWave.ContactCapacity];
        private int waveTouchedCount,waveScanOffset;private bool waveHitFighter;private float waveRecoveryUntil;
        public bool SphereWaveActive {get;private set;}
        public Vector3 SphereWaveCenter {get;private set;}
        public Vector3 SphereWaveUp {get;private set;}
        public float SphereWaveAge {get;private set;}
        public float SphereWaveRadius=>FireSphereWave.Radius(SphereWaveAge);
        public int SphereWaves {get;private set;}
        public int SphereWaveContacts=>waveTouchedCount;
        public double SphereWaveCpuMilliseconds {get;private set;}
        public bool TryReleaseSphereWave()
        {
            if(!IsAvailable||!WeaveHeld||WeaveForm!=FireWeaveForm.Sphere||WeavePower01<.999f||SphereWaveActive||clock<waveRecoveryUntil)return false;
            SphereWaveCenter=root.position+LocalUp;SphereWaveUp=LocalUp;SphereWaveAge=0;SphereWaveActive=true;
            waveRecoveryUntil=clock+FireSphereWave.RecoverySeconds;WeaveHeld=false;waveHitFighter=false;waveTouchedCount=0;waveScanOffset=0;
            Array.Clear(waveTouched,0,waveTouched.Length);SphereWaves++;
            Cue(FireAbilityEffectKind.SphereWave,SphereWaveCenter,SphereWaveUp,SphereWaveUp,1,FireSphereWave.Duration);return true;
        }
        private void ClearSphereWave()
        {SphereWaveActive=false;SphereWaveAge=0;waveTouchedCount=0;Array.Clear(waveTouched,0,waveTouched.Length);Array.Clear(waveOverlap,0,waveOverlap.Length);}
        private void StepSphereWave(float dt)
        {
            if(!SphereWaveActive)return;
            using var marker=SphereWaveMarker.Auto();double start=Time.realtimeSinceStartupAsDouble;
            SphereWaveAge=Mathf.Min(FireSphereWave.Duration,SphereWaveAge+dt);
            float radius=SphereWaveRadius;
            int count=UnityEngine.Physics.OverlapSphereNonAlloc(SphereWaveCenter,radius,waveOverlap,mask,QueryTriggerInteraction.Ignore);
            if(count==waveOverlap.Length){QuerySaturations++;SphereWaveActive=false;return;}
            int queries=0,startIndex=count>0?waveScanOffset%count:0;
            for(int scanned=0;scanned<count&&queries<FireSphereWave.ContactsPerStep;scanned++)
            {
                int i=(startIndex+scanned)%count;waveScanOffset=i+1;
                var c=waveOverlap[i];if(Self(c))continue;
                bool seen=false;for(int j=0;j<waveTouchedCount;j++)
                    if(waveTouched[j]==c||(c.attachedRigidbody!=null&&waveTouched[j]!=null&&waveTouched[j].attachedRigidbody==c.attachedRigidbody)){seen=true;break;}
                if(seen)continue;
                if(!FireColliderSurface.TryPoint(c,SphereWaveCenter,-SphereWaveUp,radius+.01f,out Vector3 point))continue;Vector3 offset=point-SphereWaveCenter;
                float distance=offset.magnitude;if(distance>radius+.01f)continue;
                queries++;if(!Unobstructed(SphereWaveCenter,point,c))continue;
                Vector3 outward=distance>.02f?offset/distance:SphereWaveUp;float strength=FireSphereWave.Strength(distance);
                if(!impact.ApplyContact(c,point,-outward,outward,.1f,8*strength,thermalShock:true))continue;
                if(waveTouchedCount==waveTouched.Length){QuerySaturations++;SphereWaveActive=false;break;}
                waveTouched[waveTouchedCount++]=c;
                if(!waveHitFighter&&c.transform.IsChildOf(rival))
                {waveHitFighter=true;Damage(38*strength,(outward+SphereWaveUp*.2f).normalized*(12*strength));}
            }
            Array.Clear(waveOverlap,0,count);
            if(SphereWaveAge>=FireSphereWave.Duration)SphereWaveActive=false;
            SphereWaveCpuMilliseconds=(Time.realtimeSinceStartupAsDouble-start)*1000;
        }
    }
}
