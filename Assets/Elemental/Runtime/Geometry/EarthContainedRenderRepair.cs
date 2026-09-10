using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Runtime.Geometry
{
    /// <summary>Cold reconstruction of contained, closed, flat-shaded fracture renders.</summary>
    public static class EarthContainedRenderRepair
    {
        public static Mesh Create(Mesh collider, out bool usedHull, out int removedDegenerate)
        {
            if(collider==null||!collider.isReadable)throw new InvalidOperationException("Fracture render repair requires a readable collider mesh.");
            float width=Mathf.Min(collider.bounds.size.x,Mathf.Min(collider.bounds.size.y,collider.bounds.size.z))*.18f;
            return Create(collider,collider,width,.22f,0u,0f,.1f,out usedHull,out removedDegenerate);
        }

        public static Mesh Create(Mesh source,Mesh collider,float width,float edgeFraction,uint seed,float variation,float alpha,
            out bool usedHull,out int removedDegenerate)
        {
            if(source==null||collider==null||!source.isReadable||!collider.isReadable)
                throw new InvalidOperationException("Contained render reconstruction needs readable source and collider meshes.");
            Mesh bevel=EarthFractureBevelMeshBuilder.Create(source,width,edgeFraction,seed,variation,alpha);
            Mesh result=null;usedHull=false;removedDegenerate=0;
            try
            {
                if(bevel!=source&&TryContainUniformly(bevel,collider))
                {
                    result=FlatCopy(bevel,out removedDegenerate);
                    if(IsClosed(result))return result;
                    Destroy(result);result=null;
                }
                // Explicit conservative fallback: exact collision hull with hard
                // geometric face normals, never a shrunken or open visual shell.
                usedHull=true;
                Mesh envelope=EarthConvexRenderEnvelope.Create(collider);
                try{result=FlatCopy(envelope,out removedDegenerate);}
                finally{Destroy(envelope);}
                if(!IsClosed(result)){Destroy(result);throw new InvalidOperationException(collider.name+": convex envelope is not closed after render reconstruction.");}
                return result;
            }
            finally{if(bevel!=source)Destroy(bevel);}
        }

        private static bool TryContainUniformly(Mesh render,Mesh collider)
        {
            Vector3[] hull=collider.vertices,vertices=render.vertices;int[] triangles=collider.triangles;
            Vector3 center=Vector3.zero;foreach(Vector3 point in hull)center+=point;center/=hull.Length;
            double scale=1;
            for(int t=0;t<triangles.Length;t+=3)
            {
                Vector3 a=hull[triangles[t]],b=hull[triangles[t+1]],c=hull[triangles[t+2]];
                if(!TryGeometricNormal(a,b,c,out Vector3 n))continue;
                // The local barycenter is inside. Support rather than a noisy fan
                // triangle offset prevents near-collinear faces inventing a plane
                // through the origin and collapsing individual vertices.
                if(Dot(n,a-center)<0)n=-n;
                double support=double.NegativeInfinity;
                foreach(Vector3 h in hull)support=Math.Max(support,Dot(n,h-center));
                if(!(support>0)||!double.IsFinite(support))return false;
                foreach(Vector3 v in vertices)
                {
                    double projection=Dot(n,v-center);
                    if(projection>support)scale=Math.Min(scale,support/projection);
                }
            }
            // More shrink would create visible collision/mesh separation.
            if(!double.IsFinite(scale)||scale<.97)return false;
            if(scale<1)
            {
                float factor=(float)(scale*(1-1e-7));
                for(int i=0;i<vertices.Length;i++)vertices[i]=center+(vertices[i]-center)*factor;
                render.vertices=vertices;render.RecalculateBounds();
            }
            return true;
        }

        public static Mesh FlatCopy(Mesh source,out int removedDegenerate)
        {
            var points=source.vertices;var sourceColors=source.colors;
            var sourceUv=new List<Vector4>[8];var targetUv=new List<Vector4>[8];
            for(int channel=0;channel<8;channel++)
            {
                var values=new List<Vector4>();source.GetUVs(channel,values);
                if(values.Count==points.Length){sourceUv[channel]=values;targetUv[channel]=new List<Vector4>();}
            }
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
            var submeshes=new List<int>[source.subMeshCount];removedDegenerate=0;
            for(int s=0;s<submeshes.Length;s++)
            {
                submeshes[s]=new List<int>();var indices=source.GetTriangles(s);
                for(int t=0;t<indices.Length;t+=3)
                {
                    int a=indices[t],b=indices[t+1],c=indices[t+2];
                    if(!TryGeometricNormal(points[a],points[b],points[c],out Vector3 normal)){removedDegenerate++;continue;}
                    Add(a);Add(b);Add(c);
                    void Add(int index)
                    {
                        submeshes[s].Add(vertices.Count);vertices.Add(points[index]);normals.Add(normal);
                        if(sourceColors.Length==points.Length)colors.Add(sourceColors[index]);
                        for(int channel=0;channel<8;channel++)targetUv[channel]?.Add(sourceUv[channel][index]);
                    }
                }
            }
            var mesh=new Mesh{name=source.name+" Closed Render",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);
            if(colors.Count==vertices.Count)mesh.SetColors(colors);
            for(int channel=0;channel<8;channel++)if(targetUv[channel]!=null)mesh.SetUVs(channel,targetUv[channel]);
            mesh.subMeshCount=submeshes.Length;
            for(int s=0;s<submeshes.Length;s++)mesh.SetTriangles(submeshes[s],s,false);
            mesh.RecalculateBounds();if(targetUv[0]!=null)mesh.RecalculateTangents();
            return mesh;
        }

        public static Mesh PreserveAuthoredDetail(Mesh source)
        {
            // Physical collision envelopes must never replace an authored exterior.
            // Drop zero-area faces and rebuild stable geometric normals, preserving
            // every other triangle, coordinate, UV, color and material submesh.
            Mesh result=FlatCopy(source,out _);
            if(IsWatertight(result))return result;
            Destroy(result);
            throw new InvalidOperationException(source.name+": authored fracture has an open boundary; repair its source rather than replacing it with a collision hull.");
        }
        public static bool IsClosed(Mesh mesh)=>CheckEdges(mesh,false);
        public static bool IsWatertight(Mesh mesh)=>CheckEdges(mesh,true);
        private static bool CheckEdges(Mesh mesh,bool allowTouchingClosedShells)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            if(triangles.Length<12)return false;
            // FlatCopy duplicates identical source coordinates exactly. Approximate
            // grid welding can merge DISTINCT short edges and invent non-manifold
            // topology; check exact geometric seam identity instead.
            var welded=new Dictionary<Vector3,int>();var ids=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 point=vertices[i];
                if(!welded.TryGetValue(point,out int id)){id=welded.Count;welded.Add(point,id);}ids[i]=id;
            }
            var edges=new Dictionary<(int,int),(int count,int direction)>();
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=ids[triangles[t]],b=ids[triangles[t+1]],c=ids[triangles[t+2]];
                if(a==b||b==c||c==a)continue; // coincident corners have no boundary contribution
                Edge(a,b);Edge(b,c);Edge(c,a);
            }
            foreach(var pair in edges)if(pair.Value.direction!=0||pair.Value.count<2||(pair.Value.count%2)!=0||(!allowTouchingClosedShells&&pair.Value.count!=2))return false;
            return edges.Count>=6;
            void Edge(int a,int b)
            {
                var key=(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(key,out var value);
                edges[key]=(value.count+1,value.direction+(a<b?1:-1));
            }
        }

        public static bool TryGeometricNormal(Vector3 a,Vector3 b,Vector3 c,out Vector3 normal)
        {
            double x1=(double)b.x-a.x,y1=(double)b.y-a.y,z1=(double)b.z-a.z;
            double x2=(double)c.x-a.x,y2=(double)c.y-a.y,z2=(double)c.z-a.z;
            double x=y1*z2-z1*y2,y=z1*x2-x1*z2,z=x1*y2-y1*x2;
            double scale=Math.Max(Math.Abs(x),Math.Max(Math.Abs(y),Math.Abs(z)));
            if(!(scale>0)||!double.IsFinite(scale)){normal=default;return false;}
            x/=scale;y/=scale;z/=scale;double length=Math.Sqrt(x*x+y*y+z*z);
            normal=new Vector3((float)(x/length),(float)(y/length),(float)(z/length));return true;
        }
        private static double Dot(Vector3 a,Vector3 b)=>(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;
        private static void Destroy(Mesh mesh){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
    }
}
