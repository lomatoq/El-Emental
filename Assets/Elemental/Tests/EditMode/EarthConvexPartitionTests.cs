using System.Collections.Generic;
using System.Linq;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthConvexPartitionTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ThinTetrahedronChildrenFillParentAndRemainClosedAndContained(int count)
        {
            float3[] points = { float3.zero, new float3(4,0,0), new float3(0,.4f,0), new float3(0,0,1) };
            var cells = EarthConvexPartitionSolver.Build(points,count);
            Assert.That(cells.Sum(x=>x.Volume) / (4f*.4f/6f), Is.InRange(.95f,1f));
            foreach(var cell in cells)
            {
                AssertClosed(cell);
                foreach(var vertex in cell.Vertices)
                {
                    float3 p=vertex+cell.Center;
                    Assert.That(math.cmin(p), Is.GreaterThanOrEqualTo(-.00001f));
                    Assert.That(p.x/4f+p.y/.4f+p.z, Is.LessThanOrEqualTo(1.00001f));
                }
            }
            // Independent half-space membership: no sample belongs to two children.
            for(int x=0;x<13;x++) for(int y=0;y<13;y++) for(int z=0;z<13;z++)
            {
                float3 point=new float3(x*4f/12f,y*.4f/12f,z/12f);
                Assert.That(cells.Count(cell=>Inside(cell,point)), Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void CubeAndRecursivePartitionsKeepAtLeastNinetyFivePercentVolume()
        {
            var points=new List<float3>();
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2) points.Add(new float3(x,y,z)*.5f);
            var first=EarthConvexPartitionSolver.Build(points.ToArray(),4);
            Assert.That(first.Sum(x=>x.Volume), Is.InRange(.95f,1f));
            foreach(var parent in first)
            {
                var children=EarthConvexPartitionSolver.Build(parent.Vertices,3);
                Assert.That(children.Sum(x=>x.Volume)/parent.Volume, Is.InRange(.95f,1f));
                foreach(var child in children) AssertClosed(child);
            }
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void RectangularArenaSourcesProduceObliqueRockFaces(int count)
        {
            var points = new List<float3>();
            for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
                points.Add(new float3(x*2f,y*.3f,z*.8f));
            var cells = EarthConvexPartitionSolver.Build(points.ToArray(), count);
            Assert.That(cells.Sum(c=>c.Volume)/(4f*.6f*1.6f), Is.InRange(.95f,1f));
            foreach (var cell in cells)
            {
                AssertClosed(cell);
                bool oblique = false;
                for (int t=0;t<cell.Triangles.Length;t+=3)
                {
                    float3 a=cell.Vertices[cell.Triangles[t]], b=cell.Vertices[cell.Triangles[t+1]], c=cell.Vertices[cell.Triangles[t+2]];
                    float3 normal=math.normalizesafe(math.cross(b-a,c-a));
                    oblique |= math.lengthsq(normal)>.9f && math.cmax(math.abs(normal))<.98f;
                }
                Assert.That(oblique, Is.True, "Broken arena blocks must have angled fracture faces, not only axis-aligned box faces.");
            }
        }

        private static bool Inside(EarthConvexPartitionCell cell,float3 point)
        {
            point-=cell.Center;
            for(int i=0;i<cell.Triangles.Length;i+=3)
            {
                float3 a=cell.Vertices[cell.Triangles[i]], b=cell.Vertices[cell.Triangles[i+1]], c=cell.Vertices[cell.Triangles[i+2]];
                if(math.dot(math.cross(b-a,c-a),point-a)>1e-8f) return false;
            }
            return true;
        }

        private static void AssertClosed(EarthConvexPartitionCell cell)
        {
            var unique=new List<float3>(); var remap=new int[cell.Vertices.Length];
            for(int i=0;i<remap.Length;i++)
            {
                int index=unique.FindIndex(v=>math.distancesq(v,cell.Vertices[i])<1e-10f);
                if(index<0) { index=unique.Count; unique.Add(cell.Vertices[i]); }
                remap[i]=index;
            }
            var edges=new Dictionary<(int,int),int>();
            for(int i=0;i<cell.Triangles.Length;i+=3) for(int j=0;j<3;j++)
            {
                int a=remap[cell.Triangles[i+j]], b=remap[cell.Triangles[i+(j+1)%3]];
                Assert.That(a,Is.Not.EqualTo(b));
                var key=(math.min(a,b),math.max(a,b));
                edges.TryGetValue(key,out int owners); edges[key]=owners+1;
            }
            Assert.That(edges.Values.All(n=>n==2),Is.True,"Every welded mesh edge must have exactly two owners.");
        }
    }
}
