using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
namespace Elemental.Presentation.Fire
{
    // One bounded procedural protective flame volume. Cosmetic field only: runtime owns defenses.
    public sealed class FireProtectionSphereRenderer:IDisposable
    {
        public const int RaySteps=Elemental.Simulation.Fire.FireSphereVolumeMath.RaySteps;
        private static int visibleGroups;public static bool HasVisibleGroups=>visibleGroups>0;
        private static readonly ProfilerMarker Marker=new("Fire.ProtectionSphere.Upload");
        private readonly GameObject visual;private readonly Mesh mesh;private readonly MeshRenderer renderer;private readonly Material material;
        private readonly Vector3[] vertices=new Vector3[8];private bool counted,disposed,qaRendering=true;
        public bool Visible=>renderer!=null&&renderer.enabled;
        public double LastStepMilliseconds {get;private set;}
        public Vector3 Center {get;private set;}public float Radius {get;private set;}
        public bool IsBoltShape {get;private set;}public Vector4 BoltShapeParameters {get;private set;}
        public FireProtectionSphereRenderer(Transform owner)
        {
            var shader=Resources.Load<Shader>("FireProtectionSphere");
            if(shader==null||!shader.isSupported)throw new NotSupportedException("Protection sphere requires the supported URP Resources/FireProtectionSphere shader.");
            material=new Material(shader){name="Continuous protective fire volume"};
            visual=new GameObject("Continuous protective fire sphere");visual.transform.SetParent(owner,false);
            renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
            mesh=new Mesh{name="Single twelve-triangle protective fire proxy"};mesh.MarkDynamic();mesh.vertices=vertices;
            mesh.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};
            visual.AddComponent<MeshFilter>().sharedMesh=mesh;
        }
        public void Step(bool active,Vector3 center,Vector3 up,float radius,float energy,float clock,bool wave=false,bool filled=false,float coverage=1.08f)
        {
            using(Marker.Auto())
            {
                double start=Time.realtimeSinceStartupAsDouble;Center=center;Radius=radius;IsBoltShape=false;material.SetVector("_BoltDirection",Vector4.zero);
                if(!active||energy<=.001f){Clear();LastStepMilliseconds=(Time.realtimeSinceStartupAsDouble-start)*1000;return;}
                float bound=radius*Elemental.Simulation.Fire.FireSphereVolumeMath.Support;
                for(int i=0;i<8;i++)vertices[i]=center+new Vector3((i&1)==0?-bound:bound,(i&2)==0?-bound:bound,(i&4)==0?-bound:bound);
                mesh.SetVertices(vertices);var bounds=new Bounds(center,Vector3.one*bound*2);mesh.bounds=bounds;renderer.bounds=bounds;
                material.SetVector("_CenterRadius",new Vector4(center.x,center.y,center.z,radius));
                material.SetVector("_UpEnergy",new Vector4(up.x,up.y,up.z,Mathf.Clamp01(energy)));material.SetFloat("_FlowTime",clock);material.SetFloat("_Wave",wave?1:0);material.SetFloat("_Filled",filled?1:0);material.SetFloat("_Coverage",Mathf.Clamp(coverage,.03f,1.08f));
                renderer.enabled=qaRendering;if(!counted){counted=true;visibleGroups++;}
                LastStepMilliseconds=(Time.realtimeSinceStartupAsDouble-start)*1000;
            }
        }
        public void StepBolt(bool active,Vector3 center,Vector3 up,Vector3 direction,float power,float age,uint id,float clock)
        {
            double start=Time.realtimeSinceStartupAsDouble;
            if(!active){Clear();LastStepMilliseconds=0;return;}
            var shape=Elemental.Simulation.Fire.FireBoltBodyShape.Evaluate(power,age,id);
            Step(true,center,up,shape.Radius,1,clock,filled:true);
            IsBoltShape=true;BoltShapeParameters=new Vector4(shape.Width,shape.Nose,shape.Rear,shape.Bend);
            material.SetVector("_BoltDirection",new Vector4(direction.x,direction.y,direction.z,1));
            material.SetVector("_BoltShape",BoltShapeParameters);material.SetVector("_BoltMotion",new Vector4(age,shape.Phase,0,0));
            LastStepMilliseconds=(Time.realtimeSinceStartupAsDouble-start)*1000;
        }
        public void SetRenderingForQa(bool enabled){qaRendering=enabled;renderer.enabled=enabled&&counted;}
        public void SetRenderingLayerForQa(int layer)=>visual.layer=Mathf.Clamp(layer,0,31);
        public void Clear(){IsBoltShape=false;renderer.enabled=false;if(counted){counted=false;visibleGroups=Mathf.Max(0,visibleGroups-1);}}
        public void Dispose(){if(disposed)return;disposed=true;Clear();UnityEngine.Object.Destroy(visual);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);}
    }
}
