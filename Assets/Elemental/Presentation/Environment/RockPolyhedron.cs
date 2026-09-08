using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Presentation.DistantScenery
{
    /// <summary>Cold, deterministic authoring data. Every part is a closed convex solid;
    /// overlapping parts are a compound visual, not a Boolean-unioned manifold.</summary>
    public sealed class RockPolyhedron
    {
        public sealed class Face
        {
            public readonly float3 Normal;
            public readonly List<float3> Points;
            public Face(float3 normal,List<float3> points){Normal=normal;Points=points;}
        }
        public readonly List<Face> Faces;
        public readonly float Epsilon;
        private RockPolyhedron(List<Face> faces,float epsilon){Faces=faces;Epsilon=epsilon;}
        public static RockPolyhedron Box(float3 minimum,float3 maximum)
        {
            var faces=new List<Face>();
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                float3 n=float3.zero;n[axis]=sign;
                var points=new List<float3>();
                for(int a=0;a<2;a++)for(int b=0;b<2;b++)
                {float3 p=minimum;p[axis]=sign>0?maximum[axis]:minimum[axis];p[(axis+1)%3]=a==0?minimum[(axis+1)%3]:maximum[(axis+1)%3];p[(axis+2)%3]=b==0?minimum[(axis+2)%3]:maximum[(axis+2)%3];points.Add(p);}
                Sort(points,n);faces.Add(new Face(n,points));
            }
            return new RockPolyhedron(faces,math.cmax(maximum-minimum)*.000004f);
        }
        public bool Clip(float3 normal,float distance,out RockPolyhedron result)
        {
            result=this;float length=math.length(normal);
            if(length<.00001f || !math.all(math.isfinite(normal)) || !math.isfinite(distance))return false;
            normal/=length;distance/=length;
            var output=new List<Face>();var intersections=new List<float3>();bool removed=false;
            foreach(Face face in Faces)
            {
                var polygon=new List<float3>();
                for(int i=0;i<face.Points.Count;i++)
                {
                    float3 a=face.Points[i],b=face.Points[(i+1)%face.Points.Count];
                    float da=math.dot(normal,a)-distance,db=math.dot(normal,b)-distance;
                    bool insideA=da<=0,insideB=db<=0;
                    if(insideA)AddDistinct(polygon,a,Epsilon);
                    else removed=true;
                    if(insideA!=insideB)
                    {
                        float3 hit=math.lerp(a,b,da/(da-db));
                        AddDistinct(polygon,hit,Epsilon);AddDistinct(intersections,hit,Epsilon);
                    }
                }
                Clean(polygon,Epsilon);
                if(polygon.Count>=3)output.Add(new Face(face.Normal,polygon));
            }
            if(!removed)return false;
            if(intersections.Count<3)return false;
            Sort(intersections,normal);Clean(intersections,Epsilon);
            if(intersections.Count<3)return false;
            output.Add(new Face(normal,intersections));
            var candidate=new RockPolyhedron(output,Epsilon);
            if(!candidate.Validate(out _))return false;
            result=candidate;return true;
        }
        public bool Validate(out double volume)
        {
            volume=0;var points=new List<float3>();var edges=new Dictionary<(int,int),int>();
            foreach(Face face in Faces)
            {
                if(face.Points.Count<3 || !math.all(math.isfinite(face.Normal)))return false;
                int fanStart=FanStart(face);float3 origin=face.Points[fanStart];float d=math.dot(face.Normal,origin);
                foreach(float3 p in face.Points)
                {
                    if(!math.all(math.isfinite(p)) || math.abs(math.dot(face.Normal,p)-d)>Epsilon*8)return false;
                    foreach(Face other in Faces)foreach(float3 q in other.Points)
                        if(math.dot(face.Normal,q)-d>Epsilon*12)return false;
                }
                for(int i=0;i<face.Points.Count;i++)
                {
                    int a=Index(points,face.Points[i],Epsilon*3),b=Index(points,face.Points[(i+1)%face.Points.Count],Epsilon*3);
                    if(a==b)return false;var key=(a,b);edges.TryGetValue(key,out int count);edges[key]=count+1;
                }
                for(int i=1;i<face.Points.Count-1;i++)
                {
                    float3 a=face.Points[(fanStart+i)%face.Points.Count],b=face.Points[(fanStart+i+1)%face.Points.Count],cross=math.cross(a-origin,b-origin);
                    if(math.dot(cross,face.Normal)<=Epsilon*Epsilon)return false;
                    volume+=(double)origin.x*((double)a.y*b.z-(double)a.z*b.y)/6+
                            (double)origin.y*((double)a.z*b.x-(double)a.x*b.z)/6+
                            (double)origin.z*((double)a.x*b.y-(double)a.y*b.x)/6;
                }
            }
            foreach(var entry in edges)
                if(entry.Value!=1 || !edges.TryGetValue((entry.Key.Item2,entry.Key.Item1),out int reverse) || reverse!=1)return false;
            return Faces.Count>=4 && volume>Epsilon*Epsilon*Epsilon;
        }
        // Choose a stable fan root maximizing its weakest triangle. A valid convex
        // polygon can have a near-collinear corner; always anchoring at vertex zero
        // produces slivers even when another exact triangulation is well conditioned.
        public static int FanStart(Face face)
        {
            int best=0;float bestMinimum=-1;
            for(int start=0;start<face.Points.Count;start++)
            {
                float weakest=float.PositiveInfinity;float3 origin=face.Points[start];
                for(int i=1;i<face.Points.Count-1;i++)
                    weakest=math.min(weakest,math.lengthsq(math.cross(face.Points[(start+i)%face.Points.Count]-origin,face.Points[(start+i+1)%face.Points.Count]-origin)));
                if(weakest>bestMinimum){best=start;bestMinimum=weakest;}
            }
            return best;
        }
        public float MinimumTriangleCross()
        {
            float minimum=float.PositiveInfinity;
            foreach(var face in Faces)
            {
                int start=FanStart(face);float3 origin=face.Points[start];
                for(int i=1;i<face.Points.Count-1;i++)
                    minimum=math.min(minimum,math.length(math.cross(face.Points[(start+i)%face.Points.Count]-origin,face.Points[(start+i+1)%face.Points.Count]-origin)));
            }
            return minimum;
        }
        public List<(Face,Face)> Adjacencies()
        {
            var result=new List<(Face,Face)>();
            for(int i=0;i<Faces.Count;i++)for(int j=i+1;j<Faces.Count;j++)
            {
                int shared=0;
                foreach(float3 a in Faces[i].Points)foreach(float3 b in Faces[j].Points)
                    if(math.lengthsq(a-b)<Epsilon*Epsilon*9)shared++;
                if(shared==2 && math.lengthsq(Faces[i].Normal+Faces[j].Normal)>.05f)result.Add((Faces[i],Faces[j]));
            }
            return result;
        }
        private static int Index(List<float3> points,float3 point,float epsilon)
        {for(int i=0;i<points.Count;i++)if(math.lengthsq(points[i]-point)<epsilon*epsilon)return i;points.Add(point);return points.Count-1;}
        private static void AddDistinct(List<float3> points,float3 point,float epsilon)
        {foreach(float3 p in points)if(math.lengthsq(p-point)<epsilon*epsilon)return;points.Add(point);}
        private static void Sort(List<float3> points,float3 normal)
        {
            float3 center=float3.zero;foreach(float3 p in points)center+=p;center/=points.Count;
            float3 u=math.normalize(math.cross(math.abs(normal.y)<.9f?new float3(0,1,0):new float3(1,0,0),normal));
            float3 v=math.cross(normal,u);
            points.Sort((a,b)=>math.atan2(math.dot(a-center,v),math.dot(a-center,u)).CompareTo(math.atan2(math.dot(b-center,v),math.dot(b-center,u))));
        }
        private static void Clean(List<float3> points,float epsilon)
        {
            bool changed=true;
            while(changed && points.Count>=3)
            {
                changed=false;
                for(int i=0;i<points.Count;i++)
                {
                    float3 a=points[(i+points.Count-1)%points.Count],b=points[i],c=points[(i+1)%points.Count];
                    if(math.lengthsq(b-a)<epsilon*epsilon || math.lengthsq(math.cross(b-a,c-b))<epsilon*epsilon*math.lengthsq(c-a))
                    {points.RemoveAt(i);changed=true;break;}
                }
            }
        }
    }

    public struct RockRandom
    {
        private uint state;
        public RockRandom(int seed){state=Hash(unchecked((uint)seed));}
        public static uint Hash(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return x==0?1:x;}
        public float Next(float a,float b){state^=state<<13;state^=state>>17;state^=state<<5;return math.lerp(a,b,(state>>8)*(1f/16777216f));}
        public int Count(int minimum,int maximum)=>minimum+(int)Next(0,maximum-minimum+1);
    }
}
