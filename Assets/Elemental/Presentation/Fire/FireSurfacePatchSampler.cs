using System.Collections.Generic;
using Elemental.Simulation.Fire;
using UnityEngine;
namespace Elemental.Presentation.Fire
{
    // Scratch buffers are shared by all burn seats. Only contacts rebuild the patch.
    internal struct FireSurfacePatchAnchor
    {public Renderer Renderer;public Mesh Mesh;public int A,B,C;public Vector3 Barycentric;}
    internal sealed class FireSurfacePatchSampler
    {
        private readonly List<Vector3> vertices=new(8192);
        private readonly List<int> indices=new(24576);
        private readonly Vector3[] probes=new Vector3[FireSurfacePatchGeometry.Capacity];
        private readonly Vector3[] points=new Vector3[FireSurfacePatchGeometry.Capacity],normals=new Vector3[FireSurfacePatchGeometry.Capacity];
        private readonly FireSurfacePatchAnchor[] candidates=new FireSurfacePatchAnchor[FireSurfacePatchGeometry.Capacity];
        private readonly float[] distances=new float[FireSurfacePatchGeometry.Capacity];
        // Unity6 BakeMesh(false) already includes the renderer scale. Request
        // compensated local vertices explicitly before TransformPoint applies scale.
        // Actual Linebreaker has2.424 world scale; false+TransformPoint doubled it.
        private Mesh baked;
        public int UnsupportedMeshes {get;private set;}
        public int Build(Transform root,List<Renderer> renderers,Vector3 contact,Vector3 normal,float radius,Vector3[] localPoints,Vector3[] localNormals,FireSurfacePatchAnchor[] anchors)
        {
            Vector3 side=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.right).normalized;
            Vector3 across=Vector3.Cross(normal,side);
            for(int i=0;i<probes.Length;i++){var offset=FireSurfacePatchGeometry.DiskOffset(i,radius);probes[i]=contact+side*offset.x+across*offset.y;distances[i]=.28f*.28f;}
            foreach(var renderer in renderers)
            {
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.bounds.SqrDistance(contact)>radius*radius)continue;
                Mesh mesh=null;Transform frame=renderer.transform;
                if(renderer is SkinnedMeshRenderer skin){if(baked==null)baked=new Mesh{name="Burn surface posed geometry scratch"};skin.BakeMesh(baked,true);mesh=baked;}
                else if(renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                if(mesh==null)continue;
                if(!mesh.isReadable){if(UnsupportedMeshes++==0)Debug.LogWarning("Surface fire cannot sample unreadable mesh '"+mesh.name+"'. Enable Read/Write on this burn receiver; no proxy-hull flames are substituted.",renderer);continue;}
                mesh.GetVertices(vertices);
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    if(mesh.GetTopology(sub)!=MeshTopology.Triangles)continue;
                    mesh.GetTriangles(indices,sub);
                    for(int t=0;t+2<indices.Count;t+=3)
                    {
                        Vector3 a=frame.TransformPoint(vertices[indices[t]]),b=frame.TransformPoint(vertices[indices[t+1]]),c=frame.TransformPoint(vertices[indices[t+2]]);
                        Vector3 n=Vector3.Cross(b-a,c-a).normalized;if(Vector3.Dot(n,normal)<.15f)continue;
                        if(!FireSurfacePatchGeometry.ClosestPoint(contact,a,b,c,out var nearest)||(new Vector3(nearest.x,nearest.y,nearest.z)-contact).sqrMagnitude>(radius+.28f)*(radius+.28f))continue;
                        for(int i=0;i<probes.Length;i++)
                        {
                            if(!FireSurfacePatchGeometry.ClosestPoint(probes[i],a,b,c,out var p))continue;
                            Vector3 world=p;float d=(world-probes[i]).sqrMagnitude;
                            if(d<distances[i]){distances[i]=d;points[i]=world;normals[i]=n;
                                Vector3 ab=b-a,ac=c-a,ap=world-a;float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),abac=Vector3.Dot(ab,ac),inv=1/(aa*bb-abac*abac);float v=(bb*Vector3.Dot(ap,ab)-abac*Vector3.Dot(ap,ac))*inv,w=(aa*Vector3.Dot(ap,ac)-abac*Vector3.Dot(ap,ab))*inv;
                                candidates[i]=new FireSurfacePatchAnchor{Renderer=renderer,Mesh=renderer is SkinnedMeshRenderer sk?sk.sharedMesh:mesh,A=indices[t],B=indices[t+1],C=indices[t+2],Barycentric=new Vector3(1-v-w,v,w)};}
                        }
                    }
                }
            }
            int count=0;
            for(int i=0;i<probes.Length;i++)
            {
                if(distances[i]>=.28f*.28f)continue;
                Vector3 local=root.InverseTransformPoint(points[i]);bool duplicate=false;
                for(int j=0;j<count;j++)if((root.TransformPoint(localPoints[j])-points[i]).sqrMagnitude<.12f*.12f){duplicate=true;break;}
                if(duplicate)continue;localPoints[count]=local;localNormals[count]=root.InverseTransformDirection(normals[i]);anchors[count]=candidates[i];count++;
            }
            return count;
        }
        public bool RefreshDeformation(Transform root,FireSurfacePatchAnchor[] anchors,int count,Vector3[] points,Vector3[] normals)
        {
            bool moved=false;
            for(int i=0;i<count;i++)
            {
                var anchor=anchors[i];if(anchor.Renderer is not SkinnedMeshRenderer skin||skin==null||skin.sharedMesh!=anchor.Mesh)continue;
                bool visited=false;for(int j=0;j<i;j++)if(anchors[j].Renderer==skin){visited=true;break;}if(visited)continue;
                if(baked==null)baked=new Mesh{name="Burn surface posed geometry scratch"};skin.BakeMesh(baked,true);baked.GetVertices(vertices);
                for(int j=i;j<count;j++)
                {
                    var link=anchors[j];if(link.Renderer!=skin)continue;
                    if(link.A>=vertices.Count||link.B>=vertices.Count||link.C>=vertices.Count)continue;
                    Vector3 a=skin.transform.TransformPoint(vertices[link.A]),b=skin.transform.TransformPoint(vertices[link.B]),c=skin.transform.TransformPoint(vertices[link.C]);
                    points[j]=root.InverseTransformPoint(a*link.Barycentric.x+b*link.Barycentric.y+c*link.Barycentric.z);
                    normals[j]=root.InverseTransformDirection(Vector3.Cross(b-a,c-a).normalized);moved=true;
                }
            }
            return moved;
        }
        public void Dispose()=>Object.Destroy(baked);
    }
}
