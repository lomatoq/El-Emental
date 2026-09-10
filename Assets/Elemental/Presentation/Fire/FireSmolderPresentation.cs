using System.Collections.Generic;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Fire;
using UnityEngine;
namespace Elemental.Presentation.Fire
{
    // Explicit owner binding. Slots are invalidated when pooled matter changes generation.
    [DefaultExecutionOrder(10000)]
    public sealed class FireSmolderPresentation:MonoBehaviour
    {
        private sealed class Seat
        {
            public Transform Root;public Collider Surface;public IEarthPhysicalTarget Target;public EarthPhysicalTargetHandle Handle;
            public readonly List<Renderer> Renderers=new(16);
            public readonly FireSurfacePatchAnchor[] Anchors=new FireSurfacePatchAnchor[16];public bool HasDeformingSurface;public float LastDeformTime=-100;
            public readonly Vector3[] BurnPoints=new Vector3[16],BurnNormals=new Vector3[16];public int BurnPointsCount,SmokeCursor;public float LastPatchTime=-100;public Vector3 PatchCenter;
            public bool PassiveHeat;public FireThermalState State;public Vector3 LocalPoint,Normal;public float Smoke;public int Flame=-1;public float BurnRemaining;
        }
        private static readonly Unity.Profiling.ProfilerMarker Marker=new("Elemental.Fire.SurfaceBurning");
        private readonly Collider[] heatNeighbors=new Collider[32];
        private readonly Seat[] seats=new Seat[32];private MaterialPropertyBlock block;
        private FireWorldImpact first,second;private ParticleSystem smoke;private Material material;
        private const int IgnitionCapacity=8;
        private FireSurfaceFlameRenderer[] flames;private Material flameMaterial;private readonly Seat[] flameOwners=new Seat[IgnitionCapacity];
        private readonly FireSurfacePatchSampler patchSampler=new();
        public int SurfaceSamples {get;private set;}
        public int UnsupportedBurnMeshes=>patchSampler.UnsupportedMeshes;
        private UnityEngine.Camera flameCamera;private FireWorldBehaviour gravitySource;
        public int ActiveIgnitions {get;private set;}
        public int IgnitionParticles {get;private set;}
        public int IgnitionQueries {get;private set;}
        public double IgnitionStepMilliseconds {get;private set;}
        private static readonly int Heat=Shader.PropertyToID("_FireHeat"),Char=Shader.PropertyToID("_FireChar");
        private static readonly int BurnPoint=Shader.PropertyToID("_FireBurnPoint"),BurnRadius=Shader.PropertyToID("_FireBurnRadius");
        public int ActiveCount {get;private set;}
        private readonly float[] lightScores=new float[4];
        public void Configure(FireWorldImpact a,FireWorldImpact b,Material dustMaterial)
        {
            block=new MaterialPropertyBlock();first=a;second=b;for(int i=0;i<seats.Length;i++)seats[i]=new Seat();
            a.ContactAccepted+=Contact;b.ContactAccepted+=Contact;a.SustainedIgnition+=IgniteSurface;b.SustainedIgnition+=IgniteSurface;a.Cleared+=ClearAll;b.Cleared+=ClearAll;
            var go=new GameObject("Bounded smolder smoke");go.transform.SetParent(transform,false);smoke=go.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.playOnAwake=false;main.maxParticles=256;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=2.5f;main.startSpeed=0;main.startSize=.5f;
            var emission=smoke.emission;emission.enabled=false;var shape=smoke.shape;shape.enabled=false;
            var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.3f,1,1.5f));
            var colors=smoke.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.32f,.27f,.22f),0),new GradientColorKey(new Color(.19f,.20f,.22f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.15f),new GradientAlphaKey(0,1)});colors.color=gradient;
            material=new Material(dustMaterial){name="Cooling smoke from dust shader"};material.SetColor("_BaseColor",Color.white);material.SetFloat("_Brightness",1);material.SetFloat("_FlowStrength",.05f);material.SetFloat("_FlipbookSpeed",1f);
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            Elemental.Runtime.World.EarthParticleSystemTuningApplier.ConfigureDustFlipbook(smoke);smoke.Play();
        }
        public void ConfigureIgnitionVisuals(FireVisualProfile profile,UnityEngine.Camera camera,int mask,FireWorldBehaviour gravity)
        {
            if(camera==null||gravity==null)throw new System.ArgumentException("Explicit burning camera and gravity required.");
            flameCamera=camera;gravitySource=gravity;
            flameMaterial=new Material(Resources.Load<Material>("FireFlipbookAccentMaterial"));
            flameMaterial.name="Mesh-area Hovl flame and smoke";
            flameMaterial.SetFloat("_BurnPalette",1);
            flames=new FireSurfaceFlameRenderer[IgnitionCapacity];
            for(int i=0;i<IgnitionCapacity;i++)flames[i]=new FireSurfaceFlameRenderer(transform,flameMaterial,i);
        }
        private void IgniteSurface(FireSurfaceContact hit)
        {
            Seat owner=AcceptContact(hit,false);if(owner==null||flames==null)return;
            int index=owner.Flame;
            if(index<0)
            {
                index=0;float remaining=float.MaxValue;
                for(int i=0;i<IgnitionCapacity;i++){if(flameOwners[i]==null){index=i;break;}if(flameOwners[i].BurnRemaining<remaining){index=i;remaining=flameOwners[i].BurnRemaining;}}
                if(flameOwners[index]!=null)flameOwners[index].Flame=-1;
                flames[index].Clear();flameOwners[index]=owner;owner.Flame=index;
            }
            owner.BurnRemaining=3f;RefreshPatch(owner);
        }
        public int CopyIgnitionLights(Vector3[] positions,Vector3[] up,float[] energy)
        {
            int capacity=Mathf.Min(4,Mathf.Min(positions.Length,Mathf.Min(up.Length,energy.Length))),count=0;
            for(int i=0;i<capacity;i++)lightScores[i]=-1;
            foreach(var owner in seats)
            {
                if(owner==null||owner.Root==null||owner.Surface==null||!owner.Surface.enabled||owner.PassiveHeat||owner.BurnPointsCount==0)continue;
                if(!owner.Root.gameObject.activeInHierarchy)continue;
                if(owner.Target!=null&&(owner.Target.TargetHandle.StableId!=owner.Handle.StableId||owner.Target.TargetHandle.Generation!=owner.Handle.Generation))continue;
                bool burning=owner.Flame>=0&&owner.BurnRemaining>0;
                // A genuinely hot contact emits light before ignition too; cold char and smoke do not.
                float heat=burning?Mathf.Clamp01(owner.State.Heat)*Mathf.Clamp01(owner.BurnRemaining/.65f):Mathf.InverseLerp(.4f,.9f,owner.State.Heat)*.55f;
                if(heat<=.01f)continue;
                Vector3 point=Vector3.zero,normal=Vector3.zero;
                for(int j=0;j<owner.BurnPointsCount;j++){point+=owner.Root.TransformPoint(owner.BurnPoints[j]);normal+=owner.Root.TransformDirection(owner.BurnNormals[j]);}
                point/=owner.BurnPointsCount;normal=normal.sqrMagnitude>.001f?normal.normalized:owner.Root.TransformDirection(owner.Normal).normalized;
                Vector3 rise=gravitySource!=null?gravitySource.SampleUp(point):owner.Root.up;
                float distance=flameCamera!=null?(point-flameCamera.transform.position).sqrMagnitude:0;
                float score=heat/(1+distance*.02f);int slot=0;
                while(slot<count&&lightScores[slot]>=score)slot++;
                if(slot>=capacity)continue;
                for(int j=Mathf.Min(count,capacity-1);j>slot;j--){lightScores[j]=lightScores[j-1];positions[j]=positions[j-1];up[j]=up[j-1];energy[j]=energy[j-1];}
                lightScores[slot]=score;positions[slot]=point+normal*.35f;up[slot]=rise;energy[slot]=heat;
                count=Mathf.Min(capacity,count+1);
            }
            return count;
        }
        private void StepIgnitions(float dt)
        {
            ActiveIgnitions=IgnitionParticles=IgnitionQueries=SurfaceSamples=0;IgnitionStepMilliseconds=0;if(flames==null)return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            for(int i=0;i<IgnitionCapacity;i++)
            {
                var owner=flameOwners[i];if(owner==null)continue;
                if(owner.Root==null){Clear(owner);continue;}
                owner.BurnRemaining=Mathf.Max(0,owner.BurnRemaining-dt);
                var point=owner.Root.TransformPoint(owner.LocalPoint);var normal=owner.Root.TransformDirection(owner.Normal).normalized;
                float heat=Mathf.Clamp01(owner.BurnRemaining/.65f)*owner.State.Heat;
                flames[i].Step(owner.Root,owner.BurnPoints,owner.BurnNormals,owner.BurnPointsCount,gravitySource.SampleUp(point),heat,Time.time,flameCamera,owner.Surface!=null?owner.Surface.attachedRigidbody:null);SurfaceSamples+=owner.BurnPointsCount;
                if(heat>.01f||flames[i].VisibleCards>0){ActiveIgnitions++;IgnitionParticles+=flames[i].VisibleCards;}
                else {flames[i].Clear();owner.Flame=-1;flameOwners[i]=null;}
            }
            IgnitionStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;
        }
        private void Contact(FireSurfaceContact hit)
        {
            var source=AcceptContact(hit,true);if(source==null)return;source.PassiveHeat=false;
            // Share one continuous world-space visual footprint across adjacent arena caches.
            // This does not publish gameplay heat, damage or secondary ignition.
            if(hit.Surface.GetComponentInParent<EarthArenaPiece>()==null)return;
            int count=Physics.OverlapSphereNonAlloc(hit.Point,1.12f,heatNeighbors,~0,QueryTriggerInteraction.Ignore);
            if(count==heatNeighbors.Length)return;
            for(int i=0;i<count;i++)
            {
                var collider=heatNeighbors[i];if(collider==null||collider==hit.Surface)continue;
                var target=collider.GetComponentInParent<EarthArenaPiece>();if(target==null)continue;
                var neighbor=AcceptContact(new FireSurfaceContact(collider,hit.Point,hit.Normal,hit.Radius,hit.Energy,target.TargetHandle),false);
                if(neighbor==null||neighbor==source)continue;
                neighbor.State=source.State;neighbor.PassiveHeat=true;
            }
        }
        private Seat AcceptContact(FireSurfaceContact hit,bool directHeat)
        {
            var target=hit.Surface.GetComponentInParent<IEarthPhysicalTarget>();
            var actor=hit.Surface.GetComponentInParent<Elemental.Runtime.Characters.PlanetMotor>();
            Transform root=actor!=null?actor.transform:target?.Body!=null?target.Body.transform:hit.Surface.transform;
            Seat seat=null;
            for(int i=0;i<seats.Length;i++)if(seats[i].Root==root){seat=seats[i];break;}
            if(seat==null)for(int i=0;i<seats.Length;i++)if(seats[i].Root==null){seat=seats[i];break;}
            if(seat==null)return null;
            if(seat.Root!=null&&seat.Target!=null&&(seat.Handle.StableId!=hit.Target.StableId||seat.Handle.Generation!=hit.Target.Generation))Clear(seat);
            if(seat.Root==null)
            {
                Clear(seat);seat.Root=root;seat.Surface=hit.Surface;seat.Target=target;seat.Handle=hit.Target;
                seat.LocalPoint=root.InverseTransformPoint(hit.Point);seat.Normal=root.InverseTransformDirection(hit.Normal);
                root.GetComponentsInChildren(false,seat.Renderers);foreach(var visual in seat.Renderers)if(visual is SkinnedMeshRenderer)seat.HasDeformingSurface=true;
            }
            // Char belongs to the original receiver patch. Following each new hit
            // moved the complete material mask across an intact floor. Local anchors
            // follow moving matter; the skinned-mesh refresh below follows its pose.
            if(directHeat)
            {
                seat.State.Ignite(hit.Energy);
                if(seat.Flame>=0)seat.BurnRemaining=3f;
                RefreshPatch(seat);
            }
            return seat;
        }
        private void RefreshPatch(Seat seat)
        {
            if(Time.time-seat.LastPatchTime<.2f)return;
            // Rebuild only when the contacted patch moves or expands, not in the frame loop.
            if(seat.BurnPointsCount>0&&(seat.LocalPoint-seat.PatchCenter).sqrMagnitude<.01f&&Time.time-seat.LastPatchTime<.75f)return;
            seat.BurnPointsCount=patchSampler.Build(seat.Root,seat.Renderers,seat.Root.TransformPoint(seat.LocalPoint),seat.Root.TransformDirection(seat.Normal).normalized,.55f+seat.State.Char*.4f,seat.BurnPoints,seat.BurnNormals,seat.Anchors);
            seat.PatchCenter=seat.LocalPoint;seat.LastPatchTime=Time.time;
        }
        private void LateUpdate()
        {
            using var marker=Marker.Auto();
            ActiveCount=0;float dt=Time.deltaTime;
            foreach(var seat in seats)
            {
                if(seat==null)continue;
                // Unity destroyed-object null must release the renderer and seat, not skip it.
                if(seat.Root==null){if(seat.Flame>=0||seat.Renderers.Count>0||seat.Target!=null)Clear(seat);continue;}
                bool valid=seat.Root.gameObject.activeInHierarchy&&seat.Surface!=null&&seat.Surface.enabled;
                if(seat.Target!=null)valid&=seat.Target.TargetHandle.StableId==seat.Handle.StableId&&seat.Target.TargetHandle.Generation==seat.Handle.Generation;
                if(!valid){Clear(seat);continue;}
                if(seat.HasDeformingSurface&&seat.BurnPointsCount>0&&Time.time-seat.LastDeformTime>=.12f)
                {if(patchSampler.RefreshDeformation(seat.Root,seat.Anchors,seat.BurnPointsCount,seat.BurnPoints,seat.BurnNormals)){seat.LocalPoint=seat.BurnPoints[0];seat.Normal=seat.BurnNormals[0];}seat.LastDeformTime=Time.time;}
                seat.State.Step(dt);if(seat.State.Char<=0&&seat.State.Heat<=0){Clear(seat);continue;}ActiveCount++;
                foreach(var renderer in seat.Renderers)if(renderer!=null){renderer.GetPropertyBlock(block);block.SetFloat(Heat,seat.State.Heat);block.SetFloat(Char,seat.State.Char);block.SetVector(BurnPoint,seat.Root.TransformPoint(seat.LocalPoint));block.SetFloat(BurnRadius,.55f+seat.State.Char*.55f);renderer.SetPropertyBlock(block);}
                if(seat.PassiveHeat)continue;
                seat.Smoke+=dt*seat.State.Heat*9;
                if(seat.Smoke>=1)
                {
                    int count=Mathf.Min(3,(int)seat.Smoke);seat.Smoke-=count;
                    // Use actual sampled surface area, point normals and world-space carrier velocity.
                    // If the mesh is unsupported, keep the explicit contact fallback rather than inventing area.
                    var body=seat.Surface.attachedRigidbody;
                    for(int emitted=0;emitted<count;emitted++)
                    {
                        int serial=seat.SmokeCursor++,index=seat.BurnPointsCount>0?serial%seat.BurnPointsCount:-1;
                        Vector3 origin=seat.Root.TransformPoint(index>=0?seat.BurnPoints[index]:seat.LocalPoint);
                        Vector3 normal=seat.Root.TransformDirection(index>=0?seat.BurnNormals[index]:seat.Normal).normalized;
                        Vector3 up=gravitySource!=null?gravitySource.SampleUp(origin):seat.Root.up;
                        Vector3 tangent=Vector3.Cross(normal,Mathf.Abs(Vector3.Dot(normal,up))<.9f?up:seat.Root.right).normalized;
                        Vector3 side=Vector3.Cross(normal,tangent);
                        float phase=serial*2.3999632f;
                        Vector3 drift=(tangent*Mathf.Cos(phase)+side*Mathf.Sin(phase))*(.2f+.3f*Mathf.Repeat(serial*.618034f,1));
                        Vector3 inherited=body!=null?Vector3.ClampMagnitude(body.GetPointVelocity(origin),8):Vector3.zero;
                        var p=new ParticleSystem.EmitParams{position=origin+normal*.015f,velocity=inherited+normal*.12f+drift+up*.2f,startSize=(.35f+seat.State.Char*.4f)*(.75f+.5f*Mathf.Repeat(serial*.414214f,1)),startColor=Color.white};
                        smoke.Emit(p,1);
                    }
                }
            }
            StepIgnitions(dt);
        }
        private void Clear(Seat seat)
        {if(seat.Flame>=0&&flames!=null){flames[seat.Flame].Clear();flameOwners[seat.Flame]=null;seat.Flame=-1;}seat.BurnRemaining=0;seat.BurnPointsCount=0;seat.HasDeformingSurface=false;seat.LastDeformTime=-100;System.Array.Clear(seat.Anchors,0,seat.Anchors.Length);seat.LastPatchTime=-100;seat.SmokeCursor=0;foreach(var renderer in seat.Renderers)if(renderer!=null){renderer.GetPropertyBlock(block);block.SetFloat(Heat,0);block.SetFloat(Char,0);renderer.SetPropertyBlock(block);}seat.Root=null;seat.Surface=null;seat.Target=null;seat.Handle=default;seat.PassiveHeat=false;seat.LocalPoint=seat.Normal=seat.PatchCenter=default;seat.Renderers.Clear();seat.State=default;seat.Smoke=0;}
        private void ClearAll(){foreach(var seat in seats)if(seat!=null)Clear(seat);if(smoke!=null)smoke.Clear();}
        private void OnDisable()=>ClearAll();
        private void OnDestroy(){patchSampler.Dispose();if(first!=null){first.ContactAccepted-=Contact;first.SustainedIgnition-=IgniteSurface;first.Cleared-=ClearAll;}if(second!=null){second.ContactAccepted-=Contact;second.SustainedIgnition-=IgniteSurface;second.Cleared-=ClearAll;}if(flames!=null)foreach(var flow in flames)flow.Dispose();if(material!=null)Destroy(material);if(flameMaterial!=null)Destroy(flameMaterial);}
    }
}
