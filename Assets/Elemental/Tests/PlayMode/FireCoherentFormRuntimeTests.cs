#if UNITY_EDITOR
using System;
using System.Collections;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class FireCoherentFormRuntimeTests
    {
        private GameObject root;
        private Camera camera;
        private FireCoherentBodyMeshBackend body;
        private Mesh mesh;
        private FirePresentationSnapshot snapshot;
        [SetUp] public void Setup()
        {
            root=new GameObject("Coherent form fixture");
            camera=new GameObject("Form camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);
            camera.enabled=false;camera.transform.position=new Vector3(4,2,-2);
            var profile=AssetDatabase.LoadAssetAtPath<FireVisualProfile>("Assets/Elemental/Content/VFX/Fire/Fire_Stream_Local.asset");
            body=new FireCoherentBodyMeshBackend(root.transform,profile);
            mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;
            snapshot=new FirePresentationSnapshot {Lifecycle=FireLifecycle.Active,Energy=1,NodeCount=1,Time=1,FreeUp=new float3(0,1,0)};
            var node=FireFieldNode.Stream(float3.zero,new float3(0,0,8),new float3(0,0,14),new float3(0,1,0));
            node.Radius=.18f;node.Lift=.2f;node.Swirl=.55f;node.NoiseSpeed=.4f;
            snapshot.Nodes[0]=node;
        }
        [Test] public void StaggeredTonguesKeepOneMuzzleAndAdvectWithoutExtendingAuthority()
        {
            body.Step(snapshot,.016f,camera);Vector3[] first=mesh.vertices;
            int count=body.ActiveRibbons*body.SectionCount*2;
            float widest=0;
            for(int i=0;i<count;i+=2)widest=Mathf.Max(widest,Vector3.Distance(first[i],first[i+1])*.5f);
            Assert.That(widest,Is.GreaterThan(.23f),"The cosmetic form must open beyond the former .18m laser half-width.");
            for(int lane=0;lane<3;lane++)
            {
                Assert.That(body.TryGetCenterlinePoint(lane,0,out Vector3 start),Is.True);
                Assert.That(start.magnitude,Is.LessThan(1e-5));
            }
            body.TryGetCenterlinePoint(0,body.SectionCount-1,out Vector3 mainTip);
            body.TryGetCenterlinePoint(1,body.SectionCount-1,out Vector3 firstTip);
            body.TryGetCenterlinePoint(2,body.SectionCount-1,out Vector3 secondTip);
            Assert.That(mainTip.z-firstTip.z,Is.GreaterThan(.2f));
            Assert.That(firstTip.z-secondTip.z,Is.GreaterThan(.2f));
            snapshot.Time+=.137f;body.Step(snapshot,.016f,camera);Vector3[] second=mesh.vertices;
            float movement=0;
            for(int i=0;i<count;i++)
            {
                movement=Mathf.Max(movement,Vector3.Distance(first[i],second[i]));
                Assert.That(second[i].z,Is.InRange(-1e-5f,8.00001f));
            }
            Assert.That(movement,Is.GreaterThan(.02f));
            body.Step(snapshot,0,camera);Vector3[] frozen=mesh.vertices;
            for(int i=0;i<count;i++)Assert.That(Vector3.Distance(second[i],frozen[i]),Is.LessThan(1e-6));
            Assert.That(snapshot.Nodes[0].Radius,Is.EqualTo(.18f));
            Assert.That(snapshot.Nodes[0].B,Is.EqualTo(new float3(0,0,8)));
        }
        [TestCase(false)] [TestCase(true)]
        public void ShortAuthorityEndpointBoundsBothEdgesWithOrWithoutFiniteContact(bool hasContact)
        {
            snapshot.Nodes[0].B=new float3(0,0,.35f);
            float3 normal=math.normalize(new float3(-.35f,0,-1));
            if(hasContact)
            {
                snapshot.ContactCount=1;
                snapshot.Contacts[0]=new FireContactPatch {Point=new float3(0,0,.35f),Normal=normal,
                    Tangent=new float3(0,1,0),Radius=2,FrontDepth=.35f,RecoveryDepth=.08f,Skin=.015f,
                    SpreadFraction=.8f,ResponseRate=22,Active=true};
            }
            body.Step(snapshot,.016f,camera);Vector3[] vertices=mesh.vertices;
            int count=body.ActiveRibbons*body.SectionCount*2;
            for(int i=0;i<count;i++)
            {
                Assert.That(vertices[i].z,Is.InRange(-1e-5f,.35001f),"Visible geometry exceeded the actual clipped source.");
                if(hasContact)Assert.That(math.dot((float3)vertices[i]-snapshot.Contacts[0].Point,normal),Is.GreaterThanOrEqualTo(.015f-1e-4f));
            }
        }
        [Test] public void WarmGeometryUpdatesDoNotAllocateManagedMemory()
        {
            Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Reset();
            if(!Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.IsSupported)Assert.Ignore("Runtime allocation counter failed its escaping positive control; native GC.Alloc evidence required.");
            for(int i=0;i<30;i++)body.Step(snapshot,.016f,camera);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++){snapshot.Time+=.016f;body.Step(snapshot,.016f,camera);}
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {body?.Dispose();if(root!=null)Object.Destroy(root);yield return null;}
    }
}
#endif
