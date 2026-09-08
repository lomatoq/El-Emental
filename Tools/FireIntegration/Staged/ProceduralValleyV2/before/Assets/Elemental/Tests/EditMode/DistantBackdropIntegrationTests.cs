using Elemental.Presentation.DistantScenery;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class DistantBackdropIntegrationTests
    {
        private GameObject root, planet;
        private DistantBackdropProfile profile;
        private Material material;
        private Mesh massif, island;
        private DistantBackdrop backdrop;
        [SetUp] public void Setup()
        {
            root = new GameObject("Backdrop test"); planet = new GameObject("Existing planet anchor");
            planet.transform.position = new Vector3(100, 25, -50);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            massif = ProceduralRockMesh.Create(4471); island = ProceduralRockMesh.Create(991, 9, true);
            profile = ScriptableObject.CreateInstance<DistantBackdropProfile>();
            profile.material = material; profile.silhouettes = new[] { massif }; profile.islandSilhouettes = new[] { island };
            profile.midMassifs = 6; profile.farMassifs = 6; profile.floatingIslands = 4;
            backdrop = root.AddComponent<DistantBackdrop>(); backdrop.Configure(profile, planet.transform, Vector3.up); backdrop.Rebuild();
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(root); Object.DestroyImmediate(planet); Object.DestroyImmediate(profile); Object.DestroyImmediate(material); Object.DestroyImmediate(massif); Object.DestroyImmediate(island); }
        [Test] public void CorrectRadiusAndNoGameplayPhysics()
        {
            Assert.That(backdrop.ScaleFactor, Is.EqualTo(55.1f / 36f).Within(.0001f));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(backdrop.GeneratedCount, Is.EqualTo(16));
        }
        [Test] public void RebuildIsDeterministicAndReplacesOnlyItsGeneratedRoot()
        {
            Transform generated = root.transform.GetChild(0);
            Vector3 first = generated.GetChild(0).position;
            backdrop.Rebuild();
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(root.transform.GetChild(0).GetChild(0).position, Is.EqualTo(first));
        }
        [Test] public void MotionIsBoundedAbsoluteAndReducedMotionRestoresBaseline()
        {
            backdrop.SetReducedMotion(true);
            Transform generated = root.transform.GetChild(0);
            Transform floating = generated.GetChild(generated.childCount - 1);
            Vector3 baseline = floating.position;
            backdrop.SetReducedMotion(false); backdrop.ApplyTime(1234);
            Vector3 first = floating.position;
            Assert.That(Vector3.Distance(first, baseline), Is.LessThanOrEqualTo(2.2f * backdrop.ScaleFactor + .001f));
            backdrop.ApplyTime(99999); backdrop.ApplyTime(1234);
            Assert.That(floating.position, Is.EqualTo(first));
            backdrop.SetReducedMotion(true); Assert.That(floating.position, Is.EqualTo(baseline));
        }
        [Test] public void ParentMovementCannotDragWorldAnchoredDecoration()
        {
            backdrop.SetReducedMotion(true);
            Transform target = root.transform.GetChild(0).GetChild(0);
            Vector3 original = target.position;
            root.transform.position += new Vector3(20, 30, 40);
            backdrop.ApplyTime(10);
            Assert.That(target.position, Is.EqualTo(original));
        }
        [Test] public void LandMassesAreStationaryWhileIslandsCanDrift()
        {
            Transform target = root.transform.GetChild(0).GetChild(0);
            backdrop.ApplyTime(0); Vector3 original = target.position;
            target.hasChanged=false;
            backdrop.ApplyTime(153); Assert.That(target.position, Is.EqualTo(original));
            Assert.That(target.hasChanged,Is.False,"Stationary massifs should not write transforms each frame.");
        }
        [Test] public void RidgesAreBroadTangentialAndExtendBelowPlanet()
        {
            foreach(Transform pivot in root.transform.GetChild(0))
            {
                if(pivot.name.StartsWith("Island_"))continue;
                Vector3 size=pivot.GetChild(0).localScale;
                Assert.That(size.x/size.y,Is.GreaterThanOrEqualTo(profile.massifWidthAspect.x));
                Assert.That(pivot.position.y-size.y*.5f,Is.LessThan(planet.transform.position.y));
                Vector3 radial=Vector3.ProjectOnPlane(pivot.position-planet.transform.position,Vector3.up).normalized;
                Assert.That(Mathf.Abs(Vector3.Dot(pivot.right,radial)),Is.LessThan(.11f));
                Assert.That(size.z,Is.LessThanOrEqualTo(Vector3.ProjectOnPlane(pivot.position-planet.transform.position,Vector3.up).magnitude*.321f));
            }
        }
        [Test] public void GeneratedTrianglesHaveFiniteOutwardNormalsAndBoundedTopology()
        {
            foreach (Mesh mesh in new[] { massif, island })
            {
                Vector3[] vertices = mesh.vertices; Vector3[] normals = mesh.normals; int[] triangles = mesh.triangles;
                Assert.That(vertices.Length, Is.LessThanOrEqualTo(1104));
                for (int i = 0; i < normals.Length; i++)
                {
                    Assert.That(float.IsNaN(normals[i].sqrMagnitude) || float.IsInfinity(normals[i].sqrMagnitude), Is.False);
                    Assert.That(normals[i].sqrMagnitude, Is.GreaterThan(.9f));
                }
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i+1]], c = vertices[triangles[i+2]];
                    Vector3 n = Vector3.Cross(b-a,c-a);
                    Assert.That(n.sqrMagnitude, Is.GreaterThan(1e-12f));
                    // Caps are identified by their emitted topology, not their slope.
                    // A tapered island side can have a near-vertical normal and is still a side.
                    int sides=mesh==massif?10:9, layers=6;
                    int sideIndexCount=(layers-1)*sides*6;
                    if(i>=sideIndexCount)
                    {
                        bool top=i>=sideIndexCount+sides*3;
                        Assert.That(Vector3.Dot(n,top?Vector3.up:Vector3.down),Is.GreaterThan(0));
                    }
                    else
                    {
                        int ring=i/(sides*6), first=ring*sides*4;
                        Vector3 lower=Vector3.zero,upper=Vector3.zero;
                        for(int j=0;j<sides;j++){lower+=vertices[first+j*4];upper+=vertices[first+j*4+1];}
                        lower/=sides;upper/=sides;
                        Vector3 centroid=(a+b+c)/3f;
                        Vector3 axis=Vector3.Lerp(lower,upper,Mathf.InverseLerp(lower.y,upper.y,centroid.y));
                        Vector3 outward=Vector3.ProjectOnPlane(centroid-axis,Vector3.up);
                        Assert.That(Vector3.Dot(n,outward),Is.GreaterThan(0),"Side winding: triangle "+i/3);
                    }
                }
            }
        }
    }
}
