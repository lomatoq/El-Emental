using System;
using System.Reflection;
using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Elemental.Tests.EditMode
{
    public sealed class DistantBackdropInPlaceTests
    {
        [Test] public void SavedTransformAndMeshIdentitySurviveRefreshAndNewBoundsConflictsReject()
        {
            var root=new GameObject("Backdrop in-place fixture");var planet=new GameObject("Planet");
            var template=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh=Object.Instantiate(template.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(template);
            var profile=ScriptableObject.CreateInstance<DistantBackdropProfile>();
            try
            {
                profile.hardPolishSupplements=false;profile.heroViewDirection=Vector3.forward;
                profile.viewLandmarks=new[]{new DistantBackdropProfile.ViewLandmark{name="MainIsland0",airborne=true}};
                var backdrop=root.AddComponent<DistantBackdrop>();backdrop.Configure(profile,planet.transform,Vector3.up);
                var generated=new GameObject("Saved generated root").transform;generated.SetParent(root.transform);
                typeof(DistantBackdrop).GetField("generatedRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(backdrop,generated);
                Transform Make(string name,Vector3 position)
                {
                    var pivot=new GameObject(name).transform;pivot.SetParent(generated);pivot.position=position;
                    pivot.rotation=Quaternion.Euler(7,29,3);pivot.localScale=new Vector3(1.1f,.9f,1.2f);
                    var lod=new GameObject("LOD0").transform;lod.SetParent(pivot,false);lod.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;return pivot;
                }
                Transform first=Make("View_MainIsland0",new Vector3(300,100,0));
                Transform second=Make("View_Ground",new Vector3(304,100,0));
                Vector3 position=first.position,scale=first.localScale;Quaternion rotation=first.rotation;
                profile.levitationPeriod=new Vector2(24,50);profile.rockingDegrees=new Vector2(.25f,.65f);
                var savedDrift=(System.Collections.IList)typeof(DistantBackdrop).GetField("drift",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(backdrop);
                Type driftType=savedDrift.GetType().GetGenericArguments()[0];object oldMotion=Activator.CreateInstance(driftType);
                driftType.GetField("target").SetValue(oldMotion,first);driftType.GetField("period").SetValue(oldMotion,11f);
                driftType.GetField("angle").SetValue(oldMotion,1.1f);driftType.GetField("phase").SetValue(oldMotion,.37f);
                driftType.GetField("spinDegreesPerSecond").SetValue(oldMotion,.05f);savedDrift.Add(oldMotion);
                var baseline=backdrop.CaptureExistingGeometry();
                backdrop.RefreshExistingGeometryAndAddSupplements(baseline);
                float tunedPeriod=(float)driftType.GetField("period").GetValue(savedDrift[0]);
                float tunedAngle=(float)driftType.GetField("angle").GetValue(savedDrift[0]);
                Assert.That(tunedPeriod,Is.InRange(24f,50f));Assert.That(tunedAngle,Is.InRange(.25f,.65f));
                Assert.That(driftType.GetField("phase").GetValue(savedDrift[0]),Is.EqualTo(.37f));
                Assert.That(driftType.GetField("spinDegreesPerSecond").GetValue(savedDrift[0]),Is.EqualTo(.05f));
                backdrop.RefreshExistingGeometryAndAddSupplements(baseline);
                Assert.That(driftType.GetField("period").GetValue(savedDrift[0]),Is.EqualTo(tunedPeriod));
                Assert.That(driftType.GetField("angle").GetValue(savedDrift[0]),Is.EqualTo(tunedAngle));
                Assert.That(generated.GetChild(0),Is.SameAs(first));Assert.That(first.position,Is.EqualTo(position));
                Assert.That(first.rotation,Is.EqualTo(rotation));Assert.That(first.localScale,Is.EqualTo(scale));
                Assert.That(first.GetChild(0).GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]*=8;mesh.vertices=vertices;mesh.RecalculateBounds();
                Assert.That(backdrop.InspectNewMeshConflicts(baseline),Is.Not.Empty);
                Assert.Throws<InvalidOperationException>(()=>backdrop.RefreshExistingGeometryAndAddSupplements(baseline));
                Assert.That(first.position,Is.EqualTo(position));Assert.That(first.rotation,Is.EqualTo(rotation));
                Assert.That(generated.childCount,Is.EqualTo(2),"Conflict must not remove or regenerate saved roots.");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(planet);Object.DestroyImmediate(mesh);Object.DestroyImmediate(profile);}
        }
    }
}
