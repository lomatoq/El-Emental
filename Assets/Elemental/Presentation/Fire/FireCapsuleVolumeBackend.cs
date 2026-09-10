using System;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.Fire
{
    // QA prototype: one local raymarched box, no particles, ribbons, grid simulation or new lights.
    public sealed class FireCapsuleVolumeBackend : IDisposable
    {
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Fire.CapsuleVolume.Upload");
        private static readonly int OriginId=Shader.PropertyToID("_VolumeOrigin");
        private static readonly int AxisId=Shader.PropertyToID("_VolumeAxis");
        private static readonly int SideId=Shader.PropertyToID("_VolumeSide");
        private static readonly int UpId=Shader.PropertyToID("_VolumeUp");
        private static readonly int ExtentId=Shader.PropertyToID("_VolumeExtent");
        private static readonly int StateId=Shader.PropertyToID("_VolumeState");
        private static readonly int TimingId=Shader.PropertyToID("_VolumeTiming");
        private static readonly int CountId=Shader.PropertyToID("_ContactCount");
        private static readonly int PointsId=Shader.PropertyToID("_ContactPointRadius");
        private static readonly int NormalsId=Shader.PropertyToID("_ContactNormalSkin");
        private static readonly int StepsId=Shader.PropertyToID("_VolumeSteps");
        private static readonly int DebugId=Shader.PropertyToID("_VolumeDebug");
        private readonly GameObject visual;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private readonly Vector3[] vertices=new Vector3[8];
        private readonly Vector4[] points=new Vector4[8],normals=new Vector4[8];
        private float fade;private bool disposed;
        private float admittedAt=float.NaN,stoppedAt=-1;
        private bool captureFrozen;
        private float artWidth=1,artSpread=1;
        public bool Visible=>renderer!=null&&renderer.enabled;
        public int ActiveVolumes {get;private set;}
        public int MaximumRaySteps {get;private set;}
        public Bounds WorldBounds {get;private set;}
        public float SourceLength {get;private set;}
        public float LifecycleFade=>fade;
        public int ContactCount {get;private set;}
        public int ProxyTriangles=>Visible?12:0;
        public FireCapsuleVolumeBackend(Transform owner,Shader shader,int steps=40)
        {
            if(shader==null||!shader.isSupported)throw new NotSupportedException("Explicit local capsule volume shader is missing or unsupported.");
            MaximumRaySteps=Mathf.Clamp(steps,32,48);
            material=new Material(shader){name="Fire capsule volume QA",renderQueue=2998};
            material.SetInt(StepsId,MaximumRaySteps);
            mesh=new Mesh{name="Fire local volume box (12 triangles)"};mesh.MarkDynamic();
            mesh.vertices=vertices;
            // Outward faces: render exits with Cull Front, including camera-inside rays.
            mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};
            visual=new GameObject("Fire capsule volume QA proxy");visual.transform.SetParent(owner,false);
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
        }
        // Explicit artist controls preserve conservative world bounds. Call this API
        // for width/spread changes; do not mutate the private material behind the owner.
        public void SetMacroShapeForQa(float widthScale,float rootOpening,float taperStart,float tongueSpread)
        {
            artWidth=Mathf.Clamp(widthScale,.65f,1.4f);artSpread=Mathf.Clamp(tongueSpread,0,1.5f);
            material.SetVector("_MacroShape",new Vector4(artWidth,Mathf.Clamp(rootOpening,.04f,.3f),Mathf.Clamp(taperStart,.2f,.8f),artSpread));
        }
        public void SetTemperatureForQa(Color edge,Color body,Color core,float absorption)
        {material.SetColor("_VolumeEdge",edge);material.SetColor("_VolumeBody",body);material.SetColor("_VolumeCore",core);material.SetFloat("_Absorption",Mathf.Clamp(absorption,.5f,6));}
        public void SetRayStepsForQa(int steps){MaximumRaySteps=Mathf.Clamp(steps,32,48);material.SetInt(StepsId,MaximumRaySteps);}
        // Freezes only this visual snapshot for identical-state sample convergence captures.
        public void SetSnapshotFrozenForQa(bool frozen)=>captureFrozen=frozen;
        public void SetDebugViewForQa(int mode)=>material.SetInt(DebugId,Mathf.Clamp(mode,0,2));
        public void Step(FirePresentationSnapshot snapshot,float delta)
        {
            using var marker=Marker.Auto();
            if(disposed)throw new ObjectDisposedException(nameof(FireCapsuleVolumeBackend));
            if(captureFrozen)return;
            if(snapshot==null||snapshot.NodeCount<0||snapshot.NodeCount>6||snapshot.ContactCount<0||snapshot.ContactCount>8||!math.isfinite(snapshot.Time)||!math.isfinite(delta)||delta<0)
                throw new ArgumentException("Volume snapshot/count/time is invalid.");
            FireFieldNode node=default;int count=0;
            for(int i=0;i<snapshot.NodeCount;i++)if(snapshot.Nodes[i].Active&&snapshot.Nodes[i].Density>0)
            {
                if(snapshot.Nodes[i].Shape!=FireShape.Capsule)throw new NotSupportedException("QA volume requires one explicit capsule, never a mixed wall/shell group.");
                node=snapshot.Nodes[i];count++;
            }
            if(count>1)throw new NotSupportedException("QA volume supports exactly one production stream capsule per group.");
            if(float.IsNaN(admittedAt))admittedAt=snapshot.Time;
            if(snapshot.Emits)fade=math.saturate(snapshot.Energy);
            else if(stoppedAt<0)stoppedAt=snapshot.Time-admittedAt;
            if(count==0||snapshot.Lifecycle==FireLifecycle.Retired||fade<=0){Clear();return;}
            if(!node.IsValid)throw new ArgumentException("Volume capsule must be finite and valid.");
            SourceLength=math.length(node.B-node.A);if(SourceLength<.005f){Clear();return;}
            float speed=math.max(1,math.length(node.Flow));
            float elapsed=snapshot.Time-admittedAt;
            if(stoppedAt>=0&&elapsed-stoppedAt>SourceLength/speed+.22f){Clear();return;}
            material.SetVector(TimingId,new Vector4(elapsed,speed,stoppedAt,0));
            float3 axis=(node.B-node.A)/SourceLength;
            float3 side=FireContactMath.SafeNormal(math.cross(node.Up,axis),FireContactMath.Tangent(axis));
            float3 up=math.normalize(math.cross(axis,side));
            // Local metric basis keeps density and bounds independent of any parent scale.
            float broadRadius=math.clamp(node.Radius*3.2f,.12f,.72f);
            float boxRadius=broadRadius*1.7f*artWidth*math.max(1,artSpread);
            material.SetVector(OriginId,V(node.A,0));material.SetVector(AxisId,V(axis,0));
            material.SetVector(SideId,V(side,0));material.SetVector(UpId,V(up,0));
            material.SetVector(ExtentId,new Vector4(boxRadius,boxRadius,SourceLength,broadRadius));
            material.SetVector(StateId,new Vector4(snapshot.Time,fade,node.Phase,math.saturate(node.Density)));
            ContactCount=snapshot.ContactCount;
            for(int i=0;i<ContactCount;i++)
            {
                var patch=snapshot.Contacts[i];points[i]=V(patch.Point,patch.Active?patch.Radius:0);normals[i]=V(patch.Normal,patch.Skin);
            }
            material.SetInt(CountId,ContactCount);material.SetVectorArray(PointsId,points);material.SetVectorArray(NormalsId,normals);
            float3 min=new float3(float.PositiveInfinity),max=new float3(float.NegativeInfinity);
            for(int i=0;i<8;i++)
            {
                float3 p=node.A+side*((i&1)==0?-boxRadius:boxRadius)+up*((i&2)==0?-boxRadius:boxRadius)+axis*((i&4)==0?0:SourceLength);
                vertices[i]=(Vector3)p;min=math.min(min,p);max=math.max(max,p);
            }
            mesh.SetVertices(vertices,0,8,MeshUpdateFlags.DontRecalculateBounds);
            WorldBounds=new Bounds((Vector3)((min+max)*.5f),(Vector3)(max-min));
            mesh.bounds=WorldBounds;renderer.bounds=WorldBounds;renderer.enabled=true;ActiveVolumes=1;
        }
        private static Vector4 V(float3 p,float w)=>new Vector4(p.x,p.y,p.z,w);
        public void Clear(){captureFrozen=false;admittedAt=float.NaN;stoppedAt=-1;fade=0;ActiveVolumes=0;ContactCount=0;if(renderer!=null)renderer.enabled=false;}
        public void Dispose(){if(disposed)return;disposed=true;Clear();if(visual!=null)UnityEngine.Object.Destroy(visual);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);}
    }
}
