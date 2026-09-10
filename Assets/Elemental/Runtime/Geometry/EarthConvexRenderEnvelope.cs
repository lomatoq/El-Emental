using System;
using System.Collections.Generic;
using UnityEngine;
namespace Elemental.Runtime.Geometry
{
    /// <summary>Cold deterministic convex envelope from collider points, independent of broken fan indices.</summary>
    public static class EarthConvexRenderEnvelope
    {
        private sealed class Face
        {
            public int A,B,C;public double X,Y,Z,D;public Vector3 Origin;
            public double Distance(Vector3 p)=>X*((double)p.x-Origin.x)+Y*((double)p.y-Origin.y)+Z*((double)p.z-Origin.z);
        }
        public static Mesh Create(Mesh source)
        {
            var raw=source.vertices;var points=new List<Vector3>();
            double unit=Math.Max(source.bounds.size.x,Math.Max(source.bounds.size.y,source.bounds.size.z));
            if(!(unit>0))throw new InvalidOperationException(source.name+": zero-size convex source.");
            double tolerance=unit*1e-7;
            foreach(var point in raw)
            {
                if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))throw new InvalidOperationException(source.name+": non-finite hull point.");
                bool exists=false;foreach(var prior in points)if(DistanceSquared(prior,point)<=tolerance*tolerance){exists=true;break;}
                if(!exists)points.Add(point);
            }
            if(points.Count<4)throw new InvalidOperationException(source.name+": fewer than four hull points.");
            int a=0,b=1,c=-1,d=-1;double best=0;
            for(int i=1;i<points.Count;i++){double value=DistanceSquared(points[a],points[i]);if(value>best){best=value;b=i;}}
            best=0;
            for(int i=0;i<points.Count;i++)
            {
                Cross(points[a],points[b],points[i],out double x,out double y,out double z);double value=x*x+y*y+z*z;
                if(value>best){best=value;c=i;}
            }
            if(c<0)throw new InvalidOperationException(source.name+": collinear hull points.");
            var plane=Make(a,b,c,points);best=0;
            for(int i=0;i<points.Count;i++){double value=Math.Abs(plane.Distance(points[i]));if(value>best){best=value;d=i;}}
            if(d<0||best<=tolerance)throw new InvalidOperationException(source.name+": coplanar hull points.");
            Vector3 inside=(points[a]+points[b]+points[c]+points[d])*.25f;
            var faces=new List<Face>{Outward(a,b,c),Outward(a,d,b),Outward(a,c,d),Outward(b,d,c)};
            for(int i=0;i<points.Count;i++)
            {
                if(i==a||i==b||i==c||i==d)continue;
                var horizon=new Dictionary<(int,int),(int A,int B,int Count)>();bool visible=false;
                for(int f=faces.Count-1;f>=0;f--)
                {
                    // Do not add an epsilon band to visibility: near-coplanar
                    // visible faces must be removed together or the horizon opens.
                    Face face=faces[f];if(face.Distance(points[i])<=0d)continue;
                    visible=true;Edge(face.A,face.B);Edge(face.B,face.C);Edge(face.C,face.A);faces.RemoveAt(f);
                }
                if(!visible)continue;
                foreach(var edge in horizon.Values)if(edge.Count==1)faces.Add(Outward(edge.A,edge.B,i));
                void Edge(int x,int y)
                {
                    var key=(Math.Min(x,y),Math.Max(x,y));horizon.TryGetValue(key,out var value);
                    horizon[key]=(x,y,value.Count+1);
                }
            }
            var triangles=new List<int>(faces.Count*3);
            foreach(var face in faces)
            {
                foreach(var point in points)if(face.Distance(point)>tolerance*8)
                    throw new InvalidOperationException(source.name+": envelope excluded an original collider point beyond 0.8ppm tolerance.");
                triangles.Add(face.A);triangles.Add(face.B);triangles.Add(face.C);
            }
            var mesh=new Mesh{name=source.name+" Convex Render Envelope"};mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
            Face Outward(int x,int y,int z){var face=Make(x,y,z,points);return face.Distance(inside)>0?Make(x,z,y,points):face;}
        }
        private static Face Make(int a,int b,int c,List<Vector3> points)
        {
            Cross(points[a],points[b],points[c],out double x,out double y,out double z);
            double length=Math.Sqrt(x*x+y*y+z*z);
            if(!(length>0)||!double.IsFinite(length))throw new InvalidOperationException("Convex envelope created a degenerate face.");
            x/=length;y/=length;z/=length;
            return new Face{A=a,B=b,C=c,Origin=points[a],X=x,Y=y,Z=z,D=x*points[a].x+y*points[a].y+z*points[a].z};
        }
        private static double DistanceSquared(Vector3 a,Vector3 b){double x=(double)a.x-b.x,y=(double)a.y-b.y,z=(double)a.z-b.z;return x*x+y*y+z*z;}
        private static void Cross(Vector3 a,Vector3 b,Vector3 c,out double x,out double y,out double z)
        {
            double x1=(double)b.x-a.x,y1=(double)b.y-a.y,z1=(double)b.z-a.z;
            double x2=(double)c.x-a.x,y2=(double)c.y-a.y,z2=(double)c.z-a.z;
            x=y1*z2-z1*y2;y=z1*x2-x1*z2;z=x1*y2-y1*x2;
        }
    }
}
