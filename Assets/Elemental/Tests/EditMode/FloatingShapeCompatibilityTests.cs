using System;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Elemental.Tests.EditMode
{
    public sealed class FloatingShapeCompatibilityTests
    {
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void BothCandidateLodsStayInsideSavedConnectedComponentEnvelopes(int family)
        {
            var profile=AssetDatabase.LoadAssetAtPath<DistantBackdropProfile>("Assets/Elemental/Content/Environment/DistantStone/DistantBackdropProfile.asset");Assert.That(profile,Is.Not.Null);
            MethodInfo components=typeof(DistantBackdrop).GetMethod("ConnectedComponentBounds",BindingFlags.Static|BindingFlags.NonPublic);Assert.That(components,Is.Not.Null);
            int index=family+6,seed=unchecked(profile.geometrySeed+index*131);
            for(int lod=0;lod<2;lod++)
            {
                string path=$"Assets/Elemental/Content/Environment/DistantStone/Meshes/Island_{index}_LOD{lod}.asset";
                Mesh original=AssetDatabase.LoadAssetAtPath<Mesh>(path);Assert.That(original,Is.Not.Null,path);
                string guid=AssetDatabase.AssetPathToGUID(path);Bounds[] saved=(Bounds[])components.Invoke(null,new object[]{original});
                Mesh candidate=ProceduralRockMesh.Group(seed,2+family%4,true,family,lod==1,profile.rockShape);
                try
                {
                    Bounds[] generated=(Bounds[])components.Invoke(null,new object[]{candidate});
                    foreach(Bounds part in generated)
                    {
                        bool contained=saved.Any(old=>Contains(old,part));
                        Assert.That(contained,Is.True,$"{path} GUID {guid}: candidate component {part} escapes every original connected-component envelope.");
                    }
                    Assert.That(candidate.bounds.min.y,Is.GreaterThanOrEqualTo(-.00001f),"Authored bottom-origin convention changed.");
                    foreach(var normal in candidate.normals)Assert.That(float.IsFinite(normal.sqrMagnitude)&&normal.sqrMagnitude>.99f,Is.True);
                    Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(guid));
                    TestContext.WriteLine($"Island {index} LOD{lod} seed{seed}: before{original.bounds} candidate{candidate.bounds}");
                }
                finally{Object.DestroyImmediate(candidate);}
            }
        }
        private static bool Contains(Bounds outer,Bounds inner)
        {
            const float tolerance=.000003f;
            Vector3 a=outer.min,b=outer.max,c=inner.min,d=inner.max;
            return c.x>=a.x-tolerance&&c.y>=a.y-tolerance&&c.z>=a.z-tolerance&&d.x<=b.x+tolerance&&d.y<=b.y+tolerance&&d.z<=b.z+tolerance;
        }
    }
}
