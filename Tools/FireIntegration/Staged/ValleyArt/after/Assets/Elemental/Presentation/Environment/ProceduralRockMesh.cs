using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Presentation.DistantScenery
{
    /// <summary>Broad, irregular carved strata. Pure deterministic mesh construction; no Blender required.</summary>
    public static class ProceduralRockMesh
    {
        public static Mesh Create(int seed,int sides=10,bool island=false,int layers=6)
        {
            sides=Mathf.Clamp(sides,5,24);layers=Mathf.Clamp(layers,3,10);
            var rng=new System.Random(seed);var rings=new Vector3[sides*layers];
            float[] radii=new float[sides],offsets=new float[sides];
            for(int j=0;j<sides;j++){radii[j]=.77f+(float)rng.NextDouble()*.28f;offsets[j]=((float)rng.NextDouble()-.5f)*.22f;}
            for(int k=0;k<layers;k++)
            {
                float y=k/(float)(layers-1)-.5f;
                float t=k/(float)(layers-1);
                float profile=island?Mathf.Lerp(.17f,1,Mathf.Pow(t,.55f)):Mathf.Lerp(1.12f,.28f,Mathf.Pow(t,1.5f));
                // Mountains have a deep continuous skirt, not a visible floating bottom.
                // Irregular upper columns break the old horizontal stripe/flat-roof silhouette.
                if(!island && k==0)y=-12f;
                if(!island)profile=Mathf.Lerp(1.10f,.16f,Mathf.Pow(t,1.05f));
                if(island && k==layers-2)profile*=1.13f;
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides+offsets[j];float r=.5f*radii[j]*profile;
                    float driftX=Mathf.Sin(k*.73f+seed)*.044f,driftZ=Mathf.Cos(k*.63f+seed*.31f)*.035f;
                    float ridge=0;
                    if(!island && k>0)
                    {
                        float jag= Mathf.Sin(a*3f+seed*.17f)*.12f+Mathf.Sin(a*5f+seed*.031f)*.065f;
                        ridge=jag*Mathf.Lerp(.35f,1f,t)+.08f*Mathf.Sin(a+seed*.23f)*t;
                        r*=1f+.12f*Mathf.Sin(a*2f+k*.7f+seed);
                        driftX+=.13f*t*t;
                    }
                    rings[k*sides+j]=new Vector3(Mathf.Cos(a)*r+driftX,y+ridge+(island&&k==layers-1?((float)rng.NextDouble()-.5f)*.065f:0),Mathf.Sin(a)*r+driftZ);
                }
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Each quad has shared normal within that broad face; no per-triangle random shading.
            for(int k=0;k<layers-1;k++)for(int j=0;j<sides;j++)
            {
                int next=(j+1)%sides;int n=vertices.Count;
                vertices.Add(rings[k*sides+j]);vertices.Add(rings[(k+1)*sides+j]);vertices.Add(rings[(k+1)*sides+next]);vertices.Add(rings[k*sides+next]);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            for(int top=0;top<2;top++)
            {
                int layer=top==1?layers-1:0;Vector3 center=Vector3.zero;for(int j=0;j<sides;j++)center+=rings[layer*sides+j];center/=sides;
                if(!island && top==1)center+=new Vector3(.012f,.20f,-.014f);
                for(int j=0;j<sides;j++)
                {
                    int n=vertices.Count;vertices.Add(center);vertices.Add(rings[layer*sides+j]);vertices.Add(rings[layer*sides+(j+1)%sides]);
                    triangles.Add(n);triangles.Add(n+(top==1?2:1));triangles.Add(n+(top==1?1:2));
                }
            }
            var mesh=new Mesh{name="EE_Rock_"+seed+"_"+sides};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
