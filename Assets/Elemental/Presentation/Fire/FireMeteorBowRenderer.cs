using System;
using Elemental.Simulation.Fire;
using UnityEngine;
using UnityEngine.Rendering;
namespace Elemental.Presentation.Fire
{
    // One thin additive sheath fitted to the current humanoid pose. Cosmetic only.
    public sealed class FireMeteorBowRenderer:IDisposable
    {
        private static readonly Unity.Profiling.ProfilerMarker Marker=new("Fire.MeteorBow.Upload");
        public const int RaySteps=FireMeteorBowMath.RaySteps;
        private readonly GameObject host;private readonly Mesh mesh;private readonly MeshRenderer renderer;private readonly Material material;
        private readonly Transform head,hips,leftShoulder,rightShoulder;
        private readonly Vector3[] vertices=new Vector3[8];
        public bool Visible=>renderer.enabled;
        public double LastStepMilliseconds {get;private set;}
        public Vector3 Center {get;private set;}
        public Vector3 HeadPoint {get;private set;}
        public Vector3 HipsPoint {get;private set;}
        public float BodyRadius {get;private set;}
        public Bounds SupportBounds=>mesh.bounds;
        public FireMeteorBowRenderer(Transform parent,Animator humanoid)
        {
            if(humanoid==null||!humanoid.isHuman)throw new ArgumentException("Meteor sheath requires the actual humanoid pose.");
            head=humanoid.GetBoneTransform(HumanBodyBones.Head);hips=humanoid.GetBoneTransform(HumanBodyBones.Hips);
            leftShoulder=humanoid.GetBoneTransform(HumanBodyBones.LeftUpperArm);rightShoulder=humanoid.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if(head==null||hips==null||leftShoulder==null||rightShoulder==null)throw new ArgumentException("Meteor sheath requires head, hips and both shoulder bones.");
            Shader shader=Resources.Load<Shader>("FireMeteorBow");
            if(shader==null||!shader.isSupported)throw new NotSupportedException("Meteor flight requires the URP Resources/FireMeteorBow shader.");
            material=new Material(shader){name="Body-bound meteor heating sheath"};
            host=new GameObject("Pose-bound meteor heating sheath");host.transform.SetParent(parent,false);
            renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
            mesh=new Mesh{name="Bounded body heating volume"};mesh.MarkDynamic();mesh.vertices=vertices;
            mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};
            host.AddComponent<MeshFilter>().sharedMesh=mesh;
        }
        public void Step(bool active,Vector3 actor,Vector3 up,Vector3 direction,float speed01,float clock)
        {
            using var scope=Marker.Auto();long started=System.Diagnostics.Stopwatch.GetTimestamp();LastStepMilliseconds=0;
            if(!active||speed01<.08f){Clear();return;}
            up=up.normalized;direction=Vector3.ProjectOnPlane(direction,up).normalized;
            if(direction.sqrMagnitude<.9f){Clear();return;}
            HeadPoint=head.position;HipsPoint=hips.position;Center=(HeadPoint+HipsPoint)*.5f;
            BodyRadius=FireMeteorBowMath.Radius(leftShoulder.position,rightShoulder.position);
            Vector3 lo=FireMeteorBowMath.Minimum(HipsPoint,HeadPoint,direction,BodyRadius);
            Vector3 hi=FireMeteorBowMath.Maximum(HipsPoint,HeadPoint,direction,BodyRadius);
            for(int i=0;i<8;i++)vertices[i]=new Vector3((i&1)==0?lo.x:hi.x,(i&2)==0?lo.y:hi.y,(i&4)==0?lo.z:hi.z);
            mesh.SetVertices(vertices);var bounds=new Bounds((lo+hi)*.5f,hi-lo);mesh.bounds=bounds;renderer.bounds=bounds;
            material.SetVector("_HipsRadius",new Vector4(HipsPoint.x,HipsPoint.y,HipsPoint.z,BodyRadius));
            material.SetVector("_HeadPower",new Vector4(HeadPoint.x,HeadPoint.y,HeadPoint.z,Mathf.SmoothStep(0,1,speed01)));
            material.SetVector("_Forward",direction);material.SetVector("_Up",up);
            material.SetVector("_BoundsMin",lo);material.SetVector("_BoundsMax",hi);
            material.SetFloat("_FlowTime",clock);renderer.enabled=true;
            LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        public void Clear()=>renderer.enabled=false;
        public void Dispose(){UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}
