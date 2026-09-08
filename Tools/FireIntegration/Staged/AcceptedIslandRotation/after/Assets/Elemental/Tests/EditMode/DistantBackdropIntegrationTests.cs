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
        [Test] public void ProceduralIslandSpinIsAbsoluteCenteredAndReducedMotionRestoresAuthoredPose()
        {
            profile.proceduralValley=true;profile.levitationAmplitude=new Vector2(.08f,.4f);
            backdrop.Rebuild();Transform floating=null;
            foreach(Transform child in root.transform.GetChild(0))if(child.name.StartsWith("Island_")){floating=child;break;}
            Assert.That(floating,Is.Not.Null);
            backdrop.SetReducedMotion(true);Vector3 baseline=floating.position;Quaternion baselineRotation=floating.rotation;
            var filter=floating.GetComponentInChildren<MeshFilter>();
            Vector3 center=filter.transform.TransformPoint(filter.sharedMesh.bounds.center);
            backdrop.SetReducedMotion(false);backdrop.ApplyTime(600);
            Vector3 first=floating.position;Quaternion rotation=floating.rotation;
            Assert.That(Quaternion.Angle(baselineRotation,rotation),Is.GreaterThan(10));
            Assert.That(Vector3.Distance(center,filter.transform.TransformPoint(filter.sharedMesh.bounds.center)),Is.LessThan(1));
            backdrop.ApplyTime(99999);backdrop.ApplyTime(600);
            Assert.That(floating.position,Is.EqualTo(first));Assert.That(floating.rotation,Is.EqualTo(rotation));
            backdrop.SetReducedMotion(true);backdrop.ApplyTime(1200);
            Assert.That(floating.position,Is.EqualTo(baseline));Assert.That(floating.rotation,Is.EqualTo(baselineRotation));
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
                Assert.That(vertices.Length, Is.LessThanOrEqualTo(24000));
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
                    // Convex closure/outward halfspaces are proven by the dedicated
                    // RockPolyhedron tests. Published compound faces preserve that
                    // exact outward normal, including their closed undersides.
                    Assert.That(Vector3.Dot(n/Mathf.Sqrt(n.sqrMagnitude),normals[triangles[i]]),Is.GreaterThan(.999f));
                    Assert.That(normals[triangles[i]],Is.EqualTo(normals[triangles[i+1]]));
                    Assert.That(normals[triangles[i]],Is.EqualTo(normals[triangles[i+2]]));
                }
            }
        }
        [Test] public void RearContinuationPreservesCombatTransformsAndHonorsBoundsExclusions()
        {
            profile.proceduralValley=true;profile.rearContinuation=false;backdrop.Rebuild();
            var before=new System.Collections.Generic.Dictionary<string,Matrix4x4>();
            foreach(Transform item in root.transform.GetChild(0))before.Add(item.name,item.localToWorldMatrix);
            profile.rearContinuation=true;backdrop.Rebuild();
            int rear=0,ground=0,floating=0;
            foreach(Transform item in root.transform.GetChild(0))
            {
                if(item.name.Contains("Island_"))floating++;else ground++;
                if(!item.name.StartsWith("Rear_")){Assert.That(item.localToWorldMatrix,Is.EqualTo(before[item.name]));continue;}
                rear++;
                Assert.That(item.position.z-planet.transform.position.z,Is.LessThan(-300));
                Assert.That(item.GetChild(0).GetComponent<MeshRenderer>().bounds.SqrDistance(planet.transform.position),
                    Is.GreaterThanOrEqualTo(Mathf.Pow(profile.planetRadius+profile.arenaExclusionPadding,2)));
            }
            Assert.That(rear,Is.InRange(1,4));Assert.That(ground,Is.LessThanOrEqualTo(12));Assert.That(floating,Is.LessThanOrEqualTo(6));
            profile.exclusionVolumes=new[]{new Bounds(new Vector3(0,0,-750),new Vector3(3000,3000,1000))};
            backdrop.Rebuild();
            foreach(Transform item in root.transform.GetChild(0))Assert.That(item.name.StartsWith("Rear_"),Is.False);
            Assert.That(backdrop.RejectedPlacements,Is.GreaterThanOrEqualTo(3));
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
        }
    }
}
