using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Elemental.Presentation.DistantScenery
{
    public static class ProceduralRockMesh
    {
        // Compatibility for existing serialized bank/installer callers. Version-two
        // authoring uses explicit Pillar/Group methods so sides/layers are not LOD seeds.
        public static Mesh Create(int seed,int sides=10,bool island=false,int layers=6)
        {
            bool low=layers<=4;
            var data=island?RockShapeBuilder.Group(seed,3,true,Math.Abs(seed%3),low,RockShapeSettings.Default):RockShapeBuilder.Pillar(seed,low,RockShapeSettings.Default);
            data.NormalizeHeight();return FromData(data,"EE_Rock_"+seed+"_"+(low?"LOD1":"LOD0"));
        }
        public static Mesh Pillar(int seed,bool low,RockShapeSettings settings)=>FromData(RockShapeBuilder.Pillar(seed,low,settings),"EE_Pillar_"+seed+"_"+(low?"LOD1":"LOD0"));
        public static Mesh Group(int seed,int pillars,bool floating,int family,bool low,RockShapeSettings settings)=>
            FromData(RockShapeBuilder.Group(seed,pillars,floating,family,low,settings),"EE_"+(floating?"Floating":"Valley")+"_"+seed+"_"+(low?"LOD1":"LOD0"));
        public static Mesh FromData(RockShapeData data,string name)
        {
            Profiler.BeginSample("DistantBackdrop.BuildRockMesh");
            try
            {
                if(!data.Validate(out _))throw new InvalidOperationException("Cannot publish invalid rock geometry.");
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
                foreach(var part in data.Parts)foreach(var face in part.Solid.Faces)
                {
                    int first=vertices.Count;
                    float3 normal=math.normalize(math.rotate(part.Rotation,face.Normal/part.Scale));
                    int fanStart=RockPolyhedron.FanStart(face);
                    for(int vertex=0;vertex<face.Points.Count;vertex++)
                    {
                        float3 p=part.Point(face.Points[(fanStart+vertex)%face.Points.Count]);vertices.Add(new Vector3(p.x,p.y,p.z));normals.Add(new Vector3(normal.x,normal.y,normal.z));
                        colors.Add(new Color(part.Tone,part.Tone,part.Tone,1));
                    }
                    for(int i=1;i<face.Points.Count-1;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
                }
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
            }
            finally{Profiler.EndSample();}
        }
    }
}
