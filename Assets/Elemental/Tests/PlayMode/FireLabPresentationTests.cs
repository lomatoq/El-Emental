#if UNITY_EDITOR
using System;
using System.Collections;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Object = UnityEngine.Object;
using System.IO;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class FireLabPresentationTests
    {
        private GameObject root;
        private FireLabDriver lab;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            var profile=AssetDatabase.LoadAssetAtPath<FireVisualProfile>("Assets/Elemental/Content/VFX/Fire/Fire_Default.asset");
            Assert.That(profile,Is.Not.Null,"Run Elemental/Fire/Build Graphs And Profile first.");
            Assert.That(profile.IsValid,Is.True);
            Assert.That(profile.CpuMaterial.GetFloat("_UseFlipbook"),Is.EqualTo(1));
            Assert.That(profile.CpuMaterial.GetTexture("_FlameAtlas"),Is.Not.Null);
            root=new GameObject("FireLab test harness"); lab=root.AddComponent<FireLabDriver>(); lab.Configure(profile);
            yield return null; yield return new WaitForSeconds(0.3f);
            Assert.That(lab.PrimaryView.IsReady,Is.True,lab.PrimaryView.BackendFailure);
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        { Time.timeScale=1; if(root!=null) Object.Destroy(root); yield return null; }
        [UnityTest]
        public IEnumerator BackendPublishesContactsAndKeepsScaledClock()
        {
            yield return new WaitForSeconds(0.8f);
            Assert.That(lab.PrimaryView.AliveParticles,Is.GreaterThan(0),"Fire backend did not retain any particles.");
            Assert.That(lab.PrimarySnapshot.ContactCount,Is.GreaterThan(0),"Direct wall has no validated finite contact.");
            if (lab.PrimaryView.ActiveBackend == FireVisualBackendSelection.CpuMesh)
            {
                var cpu = lab.PrimaryView.CpuDiagnostics;
                Assert.That(cpu.RedirectedExistingParticles,Is.GreaterThan(0),"No pre-existing particle was redirected by contact.");
                Assert.That(cpu.LastRedirectAgeAfter,Is.GreaterThan(cpu.LastRedirectAgeBefore));
                Assert.That(cpu.LastRedirectAgeBefore,Is.GreaterThan(0));
            }
            lab.SetTimeScale(0); yield return null; yield return new WaitForEndOfFrame();
            float before=lab.PrimarySnapshot.Time;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(lab.PrimarySnapshot.Time,Is.EqualTo(before).Within(0.001f));
            lab.SetTimeScale(0.1f); yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(lab.PrimarySnapshot.Time-before,Is.InRange(0.005f,0.07f));
            lab.SetTimeScale(1);
        }
        [UnityTest]
        public IEnumerator CaptureContactScenariosAndGeometryInvalidation()
        {
            foreach(var scenario in new[]{FireLabScenario.Direct,FireLabScenario.Oblique,FireLabScenario.Corner,FireLabScenario.Moving})
            {
                lab.SetScenario(scenario); yield return new WaitForSeconds(1.0f);
                Assert.That(lab.PrimaryView.AliveParticles,Is.GreaterThan(0),scenario.ToString());
                if(scenario==FireLabScenario.Corner) Assert.That(lab.PrimarySnapshot.ContactCount,Is.GreaterThanOrEqualTo(2),"Corner must publish separate finite faces.");
                Capture(scenario+"-bloom"); lab.SetBloom(false); yield return null; Capture(scenario+"-no-bloom"); lab.SetBloom(true);
            }
            lab.SetScenario(FireLabScenario.Invalidation); yield return new WaitForSeconds(2);
            Assert.That(lab.PrimarySnapshot.ContactCount,Is.GreaterThan(0)); Capture("before-invalidation");
            yield return new WaitForSeconds(1.4f);
            Assert.That(lab.PrimarySnapshot.ContactCount,Is.EqualTo(0),"Destroyed wall left a stale contact."); Capture("after-invalidation");
        }
        [UnityTest]
        public IEnumerator StopDrainsBeforeReuse()
        {
            yield return new WaitForSeconds(0.5f); int alive=lab.PrimaryView.AliveParticles;
            Assert.That(alive,Is.GreaterThan(0)); lab.StopEmission(); yield return null;
            Assert.That(lab.PrimarySnapshot.Emits,Is.False);
            yield return new WaitForSeconds(1.2f);
            Assert.That(lab.PrimaryView.AliveParticles,Is.EqualTo(0));
            if(lab.PrimaryView.CpuDiagnostics.BodyDiagnostics!=null)
                Assert.That(lab.PrimaryView.CpuDiagnostics.BodyDiagnostics.Visible,Is.False);
        }
        [UnityTest]
        public IEnumerator CpuMeshHasNoSteadyStateManagedAllocation()
        {
            var settings=AssetDatabase.LoadAssetAtPath<FireVisualProfile>("Assets/Elemental/Content/VFX/Fire/Fire_Default.asset");
            var snapshot=new FirePresentationSnapshot {Group=new FireGroupHandle(0,1), Lifecycle=FireLifecycle.Active,Seed=901,Energy=1,FreeUp=new float3(0,1,0),NodeCount=1,ContactCount=1};
            snapshot.Nodes[0]=FireFieldNode.Stream(new float3(-3,1,0),new float3(-0.3f,1,0),new float3(12,0,0),new float3(0,1,0));
            snapshot.Contacts[0]=new FireContactPatch {Point=new float3(0,1,0),Normal=new float3(-1,0,0),Tangent=new float3(0,0,1),Radius=1.5f,FrontDepth=0.35f,RecoveryDepth=0.08f,Skin=0.015f,SpreadFraction=0.8f,ResponseRate=22,Active=true};
            var timing=new double[128];
            using(var backend=new FireCpuMeshBackend(root.transform,settings))
            {
                backend.Begin(901);
                for(int i=0;i<96;i++){snapshot.Time+=1f/60;backend.Step(snapshot,1f/60,320,lab.LabCamera);}
                long baseline=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<128;i++)
                {
                    snapshot.Time+=1f/60;long start=System.Diagnostics.Stopwatch.GetTimestamp();
                    backend.Step(snapshot,1f/60,320,lab.LabCamera);
                    timing[i]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                }
                long allocated=GC.GetAllocatedBytesForCurrentThread()-baseline;
                Assert.That(allocated,Is.EqualTo(0),"CPU mesh steady-state publication allocated managed memory.");
                Assert.That(backend.RedirectedExistingParticles,Is.GreaterThan(0));
                Array.Sort(timing); Directory.CreateDirectory("Logs/FireLab");
                File.WriteAllText("Logs/FireLab/cpu-mesh-editor-sample.json",JsonUtility.ToJson(new CpuSample
                {
                    hardware=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                    capacity=backend.Capacity,alive=backend.AliveCount,samples=timing.Length,allocatedBytes=allocated,
                    p50Milliseconds=timing[64],p95Milliseconds=timing[121],redirectedExisting=backend.RedirectedExistingParticles
                },true));
            }
            yield return null;
        }
        [Serializable] private sealed class CpuSample
        {
            public string hardware,api;
            public int capacity,alive,samples,redirectedExisting;
            public long allocatedBytes;
            public double p50Milliseconds,p95Milliseconds;
        }
        private void Capture(string name)
        {
            const int width=1280,height=720; var camera=lab.LabCamera;
            var previous=camera.targetTexture; var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf); var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                Directory.CreateDirectory("Logs/FireLab/Captures"); File.WriteAllBytes("Logs/FireLab/Captures/"+name+".png",texture.EncodeToPNG());
            }
            finally { camera.targetTexture=previous; RenderTexture.active=active; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); }
        }
    }
}
#endif



