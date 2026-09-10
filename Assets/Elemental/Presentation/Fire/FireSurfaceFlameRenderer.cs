using UnityEngine;
using UnityEngine.Rendering;
namespace Elemental.Presentation.Fire
{
    // Hovl motion flipbooks rooted at distinct points of the heated visible mesh.
    internal sealed class FireSurfaceFlameRenderer
    {
        private static int visibleGroups;public static bool HasVisibleGroups=>visibleGroups>0;private bool counted;
        private const int HotCards=16,SmokeCards=12,Cards=HotCards+SmokeCards;
        private readonly SubMeshDescriptor[] submeshes=new SubMeshDescriptor[2];
        private readonly GameObject host;private readonly Mesh mesh;private readonly MeshRenderer renderer;
        private readonly Vector3[] vertices=new Vector3[Cards*4];
        private readonly Vector4[] data=new Vector4[Cards*4],planes=new Vector4[Cards*4];
        private readonly Vector4[] billboardCenter=new Vector4[Cards*4],billboardAxis=new Vector4[Cards*4];
        private readonly Vector2[] uv=new Vector2[Cards*4];
        private readonly float phase;
        private struct SmokePuff
        {
            public Vector3 Position,Velocity,Up,Normal;public Vector4 Clip;
            public float Age,Lifetime,Width,Seed,NextBirth,Strength;public bool Active;
        }
        private readonly SmokePuff[] smokePuffs=new SmokePuff[SmokeCards];
        private readonly Vector3[] previousAnchors=new Vector3[HotCards];
        private float lastTime=-1;private bool hasPreviousAnchors;
        public bool Visible=>renderer.enabled;
        public int VisibleCards {get;private set;}
        public FireSurfaceFlameRenderer(Transform parent,Material material,int index)
        {
            phase=index*2.17f;host=new GameObject("Mesh area fire and cooling smoke "+index);host.transform.SetParent(parent,false);
            mesh=new Mesh{name="Sixteen heated surface tongues and twelve smaller smoke wisps"};mesh.MarkDynamic();
            var triangles=new int[Cards*6];
            for(int i=0;i<Cards;i++){int n=i*4;uv[n]=new Vector2(0,0);uv[n+1]=new Vector2(1,0);uv[n+2]=new Vector2(1,1);uv[n+3]=new Vector2(0,1);int t=i*6;triangles[t]=n;triangles[t+1]=n+1;triangles[t+2]=n+2;triangles[t+3]=n;triangles[t+4]=n+2;triangles[t+5]=n+3;}
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;submeshes[0]=new SubMeshDescriptor(0,HotCards*6);submeshes[1]=new SubMeshDescriptor(HotCards*6,SmokeCards*6);mesh.SetSubMeshes(submeshes,0,2,MeshUpdateFlags.DontRecalculateBounds);
            host.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=host.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{material,Resources.Load<Material>("FireSmokeDustMaterial")};
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.enabled=false;
        }
        public void Step(Transform root,Vector3[] points,Vector3[] normals,int count,Vector3 up,float heat,float time,UnityEngine.Camera camera,Rigidbody sourceBody=null)
        {
            if(count==0){Clear();return;}
            float elapsed=lastTime<0?0:Mathf.Max(0,time-lastTime),dt=Mathf.Min(elapsed,.05f);
            if(lastTime<0)for(int i=0;i<SmokeCards;i++)smokePuffs[i].NextBirth=time+i*.075f;
            lastTime=time;
            Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
            VisibleCards=0;
            for(int i=0;i<Cards;i++)
            {
                bool cooling=i>=HotCards;int anchor=cooling?(i-HotCards)*count/SmokeCards:i;bool active=anchor<count&&heat>.01f;
                anchor=Mathf.Min(anchor,count-1);Vector3 normal=root.TransformDirection(normals[anchor]).normalized;
                Vector3 point=root.TransformPoint(points[anchor])+normal*.07f;
                float seed=phase+i*2.39996f,cycle=Mathf.Repeat(time*(cooling?.35f:.72f+.12f*Mathf.Sin(seed))+seed,1);
                Vector3 facing=(camera.transform.position-point).normalized;
                Vector3 rise=(up+normal*.35f).normalized;
                Vector3 side=Vector3.Cross(rise,facing).normalized;if(side.sqrMagnitude<.1f)side=camera.transform.right;
                float width=cooling?.32f+cycle*.28f:.28f+.09f*(.5f+.5f*Mathf.Sin(seed));
                float height=cooling?width*2:.68f+.45f*(.5f+.5f*Mathf.Sin(seed*3.7f));
                height*=Mathf.Lerp(.65f,1,Mathf.Clamp01(heat));
                Vector3 drift=side*Mathf.Sin(time*2.7f+seed)*(cooling?.16f:.065f);
                Vector4 clip=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,root.TransformPoint(points[anchor]))-.015f);
                if(cooling)
                {
                    ref var puff=ref smokePuffs[i-HotCards];
                    if(puff.Active)
                    {
                        puff.Age+=elapsed;
                        Vector3 tangent=Vector3.Cross(puff.Up,puff.Normal);
                        if(tangent.sqrMagnitude<.01f)tangent=Vector3.Cross(puff.Up,Vector3.right);
                        tangent.Normalize();
                        Vector3 curl=tangent*Mathf.Sin(puff.Age*2.7f+puff.Seed)*.26f+Vector3.Cross(puff.Up,tangent)*Mathf.Cos(puff.Age*1.9f+puff.Seed)*.18f;
                        puff.Velocity=Vector3.Lerp(puff.Velocity,puff.Up*.55f+curl,1-Mathf.Exp(-dt*1.2f));
                        puff.Position+=puff.Velocity*dt;
                        if(puff.Age>=puff.Lifetime)puff.Active=false;
                    }
                    if(!puff.Active&&heat>.01f&&time>=puff.NextBirth)
                    {
                        Vector3 sourcePoint=root.TransformPoint(points[anchor]);
                        Vector3 inherited=sourceBody!=null?sourceBody.GetPointVelocity(sourcePoint):hasPreviousAnchors&&dt>0?(sourcePoint-previousAnchors[anchor])/dt:Vector3.zero;
                        puff.Position=point;puff.Up=up;puff.Normal=normal;puff.Clip=clip;
                        puff.Seed=seed+time*.37f;puff.Age=0;puff.Lifetime=1.4f+.45f*(.5f+.5f*Mathf.Sin(seed*3.1f));
                        puff.Width=.22f+.14f*(.5f+.5f*Mathf.Sin(seed*4.3f));puff.Strength=Mathf.Clamp01(heat*1.5f);
                        puff.Velocity=Vector3.ClampMagnitude(inherited,8)*.65f+normal*.18f+up*.4f;
                        puff.Active=true;puff.NextBirth=time+puff.Lifetime;
                    }
                    active=puff.Active;cycle=Mathf.Clamp01(puff.Age/Mathf.Max(.01f,puff.Lifetime));
                    point=puff.Position;rise=puff.Up;normal=puff.Normal;clip=puff.Clip;seed=puff.Seed;
                    width=puff.Width*Mathf.Lerp(.62f,1.35f,cycle);height=width*Mathf.Lerp(1.1f,1.7f,.5f+.5f*Mathf.Sin(seed));drift=Vector3.zero;
                    facing=(camera.transform.position-point).normalized;side=Vector3.Cross(rise,facing).normalized;
                    if(side.sqrMagnitude<.1f)side=camera.transform.right;
                }
                if(!cooling){float evolve=Mathf.Sin(cycle*Mathf.PI);width*=.78f+.24f*evolve;height*=.7f+.3f*evolve;rise=(rise+side*Mathf.Sin(seed+cycle*1.8f)*.23f).normalized;point-=rise*height*.15f;}
                Vector3 tip=point+rise*height+drift;
                float alpha=active?(cooling?smokePuffs[i-HotCards].Strength*.38f*Mathf.Sin(cycle*Mathf.PI):Mathf.Clamp01(heat*1.5f)*.72f*Mathf.SmoothStep(0,1,Mathf.Clamp01(cycle/.12f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,cycle)))):0;
                float age=cooling?.2f+cycle*.75f:.05f+cycle*.78f;
                int n=i*4;
                vertices[n]=host.transform.InverseTransformPoint(point-side*width);
                vertices[n+1]=host.transform.InverseTransformPoint(point+side*width);
                vertices[n+2]=host.transform.InverseTransformPoint(tip+side*width);
                vertices[n+3]=host.transform.InverseTransformPoint(tip-side*width);
                Vector3 cardCenter=(point+tip)*.5f,cardAxis=(tip-point).normalized;
                for(int k=0;k<4;k++){billboardCenter[n+k]=new Vector4(cardCenter.x,cardCenter.y,cardCenter.z,width);billboardAxis[n+k]=new Vector4(cardAxis.x,cardAxis.y,cardAxis.z,(tip-point).magnitude*.5f);data[n+k]=new Vector4(age,cooling?1:0,seed,alpha);planes[n+k]=clip;if(active){min=Vector3.Min(min,vertices[n+k]);max=Vector3.Max(max,vertices[n+k]);}}
                if(active)VisibleCards++;
            }
            for(int i=0;i<count&&i<HotCards;i++)previousAnchors[i]=root.TransformPoint(points[i]);hasPreviousAnchors=true;
            if(VisibleCards==0){renderer.enabled=false;if(counted){counted=false;visibleGroups--;}return;}
            mesh.SetVertices(vertices);mesh.SetUVs(1,data);mesh.SetUVs(2,planes);mesh.SetUVs(4,billboardCenter);mesh.SetUVs(5,billboardAxis);mesh.bounds=new Bounds((min+max)*.5f,max-min+Vector3.one*4);renderer.enabled=true;if(!counted){counted=true;visibleGroups++;}
        }
        public void Clear(){if(counted){counted=false;visibleGroups=Mathf.Max(0,visibleGroups-1);}renderer.enabled=false;VisibleCards=0;lastTime=-1;hasPreviousAnchors=false;System.Array.Clear(smokePuffs,0,SmokeCards);}
        public void Dispose(){Clear();Object.Destroy(mesh);Object.Destroy(host);}
    }
}
