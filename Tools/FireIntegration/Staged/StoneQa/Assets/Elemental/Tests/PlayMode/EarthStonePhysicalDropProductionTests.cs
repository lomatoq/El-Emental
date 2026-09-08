using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    /// <summary>Real PhysX contact -> released decor -> hub -> saved presenter. No direct emission.</summary>
    public sealed class EarthStonePhysicalDropProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/StonePhysicalDrop";
        private const int Layer = 31;
        private const float Clearance = .04f;
        private Scene _scene, _previous;
        private float _originalTimeScale;
        private Report _report;
        [Serializable] private sealed class Sample
        {
            public string name;
            public uint source;
            public float massBefore, massAfter, canonicalMass, volume, clearance, closingSpeed, collisionImpulse, gravity;
            public int collisions, impactEvents, firstImpactDust, firstImpactChips, dustAdmitted, chipsAdmitted, peakNativeDust, peakNativeChips, meshMask, changedPixels;
            public int impactEventsAfterSettled;
            public double presenterCallbackPeakMs, presenterCallbackTotalMs, markerMeanMs, markerPeakMs;
            public long presenterCallbackAllocatedBytes;
            public bool markerValid, passed;
        }
        [Serializable] private sealed class Report
        {
            public string utc, stage, limits = "Test-authored stones on a flat tangent support; saved gravity, mass policy, meshes, materials and presenter. Effects injection isolated from unrelated scene emitters. Callback bracket includes delegate overhead. Render captures excluded from callback cost. Not full-frame/GPU performance acceptance.";
            public bool passed;
            public int rendererMeshCount;
            public Sample[] cases = { new Sample { name="light",source=0xEA7101 }, new Sample { name="heavy",source=0xEA7102 } };
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            _previous=SceneManager.GetActiveScene(); _originalTimeScale=Time.timeScale;
            _report=new Report { utc=DateTime.UtcNow.ToString("O"),stage="loading" };
            Directory.CreateDirectory(Folder);
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded,Is.False,"Run from an empty test scene; production arena must not already be loaded.");
            yield return SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Additive);
            _scene=SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(_scene);
            var gate=All<EarthSceneReadinessGate>().Single();
            double until=Time.realtimeSinceStartupAsDouble+140;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<until) yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            foreach(var bot in All<EarthMvpBotController>()) bot.enabled=false;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if(_report!=null) File.WriteAllText(Folder+"/evidence.json",JsonUtility.ToJson(_report,true));
            if(_previous.IsValid()&&_previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if(_scene.IsValid()&&_scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
            Time.timeScale=_originalTimeScale;
        }
        [UnityTest] public IEnumerator EqualLowDropsProduceMassAwareDustFromActualContactsAndVisibleChips()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Saved production references require Editor PlayMode."); yield break;
#else
            var pool=All<EarthRockDebrisPool>().First();
            var kernel=All<EarthMatterKernelBehaviour>().Single();
            var gravity=All<GravityWorldBehaviour>().First();
            var presenter=All<EarthMaterialFeedbackPresenter>().Single();
            var fields=new SerializedObject(presenter);
            var tuning=fields.FindProperty("profile").objectReferenceValue as EarthEffectsTuningProfile;
            var dust=fields.FindProperty("dust").objectReferenceValue as ParticleSystem;
            var chips=fields.FindProperty("chips").objectReferenceValue as ParticleSystem;
            var fracture=fields.FindProperty("fractureDust").objectReferenceValue as ParticleSystem;
            var chipMesh=fields.FindProperty("chipMesh").objectReferenceValue as Mesh;
            var planet=fields.FindProperty("planetCenter").objectReferenceValue as Transform;
            Assert.That(tuning,Is.Not.Null); Assert.That(dust,Is.Not.Null); Assert.That(chips,Is.Not.Null); Assert.That(planet,Is.Not.Null);
            var rig=New("Stone physical drop QA");
            var hub=rig.AddComponent<EarthMaterialFeedbackHub>(); hub.Configure(tuning,null);
            Sample active=null; long callbackStart=0, allocatedStart=0;
            // Listener order brackets the actual presenter callback, not synthetic Emit calls.
            hub.Presented+=cue=>
            {
                if(active==null||cue.SourceId!=active.source||cue.Kind!=EarthMaterialFeedbackKind.Impact) return;
                allocatedStart=GC.GetAllocatedBytesForCurrentThread(); callbackStart=System.Diagnostics.Stopwatch.GetTimestamp();
            };
            presenter.Configure(hub,tuning,planet,dust,chips,chipMesh,fracture);
            hub.Presented+=cue=>
            {
                if(active==null||cue.SourceId!=active.source||cue.Kind!=EarthMaterialFeedbackKind.Impact) return;
                long end=System.Diagnostics.Stopwatch.GetTimestamp();
                active.presenterCallbackAllocatedBytes+=GC.GetAllocatedBytesForCurrentThread()-allocatedStart;
                double ms=(end-callbackStart)*1000d/System.Diagnostics.Stopwatch.Frequency;
                active.presenterCallbackTotalMs+=ms; active.presenterCallbackPeakMs=Math.Max(active.presenterCallbackPeakMs,ms);
                if(active.impactEvents==0) { active.firstImpactDust=cue.DustCount; active.firstImpactChips=cue.ChipCount; }
                active.impactEvents++; active.dustAdmitted+=cue.DustCount; active.chipsAdmitted+=cue.ChipCount;
            };
            foreach(var ps in new[]{dust,chips,fracture}) if(ps!=null) ps.gameObject.layer=Layer;
            var chipRenderer=chips.GetComponent<ParticleSystemRenderer>();
            _report.rendererMeshCount=chipRenderer.meshCount;
            Assert.That(_report.rendererMeshCount,Is.EqualTo(4));
            var meshes=new Mesh[4]; Assert.That(chipRenderer.GetMeshes(meshes),Is.EqualTo(4));
            var motor=All<PlanetMotor>().First();
            Vector3 up=(motor.transform.position-planet.position).normalized;
            Vector3 right=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.forward).normalized;
            Vector3 forward=Vector3.Cross(right,up);
            // Elevated isolated pad avoids damaging the saved arena and guarantees a visible contact plane.
            Vector3 contact=motor.transform.position+right*9f+up*4f;
            up=(contact-planet.position).normalized;
            right=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.forward).normalized;
            forward=Vector3.Cross(right,up);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="QA tangent support";
            SceneManager.MoveGameObjectToScene(floor,_scene); floor.layer=Layer;
            floor.transform.SetPositionAndRotation(contact-up*.2f,Quaternion.LookRotation(forward,up));
            floor.transform.localScale=new Vector3(12,.4f,10); floor.GetComponent<Renderer>().sharedMaterial=pool.StoneMaterial;
            var camera=New("Stone QA camera").AddComponent<UnityEngine.Camera>();
            camera.enabled=false; camera.cullingMask=1<<Layer; camera.fieldOfView=42; camera.nearClipPlane=.03f; camera.farClipPlane=60;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.11f,.13f,.15f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            Vector3 focus=contact+up*.65f;
            camera.transform.SetPositionAndRotation(focus-forward*5.5f+right*1.8f+up*1.9f,Quaternion.LookRotation(focus-(focus-forward*5.5f+right*1.8f+up*1.9f),up));
            var native=new ParticleSystem.Particle[chips.main.maxParticles];
            for(int caseIndex=0;caseIndex<2;caseIndex++)
            {
                _report.stage="drop-"+_report.cases[caseIndex].name;
                foreach(var ps in new[]{dust,chips,fracture}) if(ps!=null) ps.Clear();
                active=_report.cases[caseIndex]; active.clearance=Clearance;
                var source=pool.ResolveShapeVariant(0);
                Assert.That(source,Is.Not.Null);
                var stone=New("QA "+active.name+" physical stone"); stone.SetActive(false); stone.layer=Layer;
                stone.transform.rotation=Quaternion.LookRotation(forward,up);
                float longest=Mathf.Max(source.bounds.size.x,Mathf.Max(source.bounds.size.y,source.bounds.size.z));
                float extent=caseIndex==0?.8f:1.15f;
                stone.transform.localScale=Vector3.one*(extent/longest);
                stone.AddComponent<MeshFilter>().sharedMesh=source;
                stone.AddComponent<MeshRenderer>().sharedMaterial=pool.StoneMaterial;
                var shape=stone.AddComponent<MeshCollider>(); shape.sharedMesh=source; shape.convex=true;
                var body=stone.AddComponent<Rigidbody>(); body.interpolation=RigidbodyInterpolation.None;
                var gravityBody=stone.AddComponent<GravityBody>(); gravityBody.Configure(gravity,body);
                var rock=stone.AddComponent<EarthDestructibleDecorRock>();
                rock.Configure(active.source,body,shape,gravityBody,pool,extent*.5f,100000f,true);
                rock.ConfigureMaterialFeedback(hub);
                stone.transform.position=contact+up*(Clearance-source.bounds.min.y*stone.transform.localScale.y);
                stone.SetActive(true); UnityEngine.Physics.SyncTransforms();
                // Startup may prepare cached fracture, but the body remains anchored above the pad.
                yield return null;
                var policy=kernel.MassPolicy;
                active.volume=EarthMatterMassRuntime.EstimateColliderVolume(shape);
                active.massBefore=body.mass;
                Assert.That(active.massBefore,Is.EqualTo(EarthMatterMassRuntime.ResolveFromCollider(shape,in policy)).Within(.01f));
                var identity=stone.GetComponent<EarthMatterIdentity>();
                var pose=new EarthMatterPose((float3)body.position,new quaternion(body.rotation.x,body.rotation.y,body.rotation.z,body.rotation.w));
                var record=new EarthMatterRecord { Phase=EarthMatterPhase.FreeDynamic, Representation=EarthRepresentationTier.SecondaryPhysical,
                    Material=EarthMaterialKind.Stone,Shape=EarthShapeSemantic.NaturalRock,Volume=active.volume,Mass=active.massBefore,
                    Integrity=1f,RestPose=pose,CurrentPose=pose,Source=new EarthSourceProvenance(EarthSourceKind.Fragment,active.source,1,-1,0,float3.zero,0,EarthProvenanceFlags.None) };
                Assert.That(identity.Configure(kernel,in record,body),Is.True);
                Assert.That(identity.TryRead(out var registered),Is.True); active.canonicalMass=registered.Mass;
                // Real public release path: zero inherited velocity, normal planet gravity and actual collision callbacks.
                rock.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
                rock.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
                // Grab smoke is unrelated to impact. Clear it before gravity can take the first physics step.
                hub.FlushPending(); foreach(var ps in new[]{dust,chips,fracture}) if(ps!=null) ps.Clear();
                Capture(camera,active.name+"-00-before");
                double until=Time.realtimeSinceStartupAsDouble+4;
                while(active.impactEvents==0&&Time.realtimeSinceStartupAsDouble<until) yield return null;
                Assert.That(active.impactEvents,Is.GreaterThan(0),"Real low-drop contact did not produce an Impact cue.");
                Assert.That(rock.ObservedCollisionCount,Is.GreaterThan(0));
                Assert.That(rock.LastCollisionCollider,Is.EqualTo(floor.GetComponent<Collider>()));
                Assert.That(rock.LastCollisionApproach,Is.GreaterThanOrEqualTo(EarthStoneImpactDust.MinimumClosingSpeed));
                Assert.That(rock.IsShattered,Is.False,"This is surviving-impact feedback QA, not a forced fracture test.");
                active.closingSpeed=rock.LastCollisionApproach; active.collisionImpulse=rock.LastCollisionImpulse;
                active.gravity=gravityBody.LastAcceleration.magnitude;
                float started=Time.time; int snapshot=0; float[] moments={0f,.08f,.2f,.5f};
                using(var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"Elemental.Earth.MaterialParticles",128))
                {
                    int frames=0;
                    while(Time.time-started<1.1f)
                    {
                        active.peakNativeDust=Math.Max(active.peakNativeDust,dust.particleCount);
                        int count=chips.GetParticles(native); active.peakNativeChips=Math.Max(active.peakNativeChips,count);
                        for(int i=0;i<count;i++) active.meshMask|=1<<native[i].GetMeshIndex(chips);
                        if(snapshot<moments.Length&&Time.time-started>=moments[snapshot])
                        {
                            var on=Capture(camera,active.name+"-"+(snapshot+1)+"-effects-on");
                            SetRenderers(dust,chips,fracture,false);
                            Color32[] off;
                            try { off=Capture(camera,active.name+"-"+(snapshot+1)+"-effects-off"); }
                            finally { SetRenderers(dust,chips,fracture,true); }
                            int changed=0;
                            for(int p=0;p<on.Length;p++) if(Math.Abs(on[p].r-off[p].r)+Math.Abs(on[p].g-off[p].g)+Math.Abs(on[p].b-off[p].b)>5) changed++;
                            active.changedPixels=Math.Max(active.changedPixels,changed); snapshot++;
                        }
                        yield return null;
                        double ms=recorder.LastValue/1000000d; active.markerMeanMs+=ms; active.markerPeakMs=Math.Max(active.markerPeakMs,ms); frames++;
                    }
                    active.markerValid=recorder.Valid; if(frames>0) active.markerMeanMs/=frames;
                }
                active.collisions=rock.ObservedCollisionCount; active.massAfter=body.mass;
                Assert.That(identity.TryRead(out var after),Is.True); Assert.That(after.Mass,Is.EqualTo(active.canonicalMass).Within(.001f));
                Assert.That(active.massAfter,Is.EqualTo(active.massBefore).Within(.001f));
                Assert.That(active.peakNativeDust,Is.GreaterThan(0)); Assert.That(active.peakNativeChips,Is.GreaterThan(0));
                Assert.That(active.changedPixels,Is.GreaterThan(32),"Actual admitted particles must affect rendered pixels.");
                double settleUntil=Time.realtimeSinceStartupAsDouble+8;
                while(!body.IsSleeping()&&Time.realtimeSinceStartupAsDouble<settleUntil) yield return new WaitForFixedUpdate();
                Assert.That(body.IsSleeping(),Is.True,"A stationary stone must settle before the no-repeat check.");
                int impactBeforeRest=active.impactEvents; yield return new WaitForSeconds(.5f);
                active.impactEventsAfterSettled=active.impactEvents-impactBeforeRest;
                Assert.That(active.impactEventsAfterSettled,Is.Zero);
                active.passed=true; active=null; UnityEngine.Object.Destroy(stone); yield return null;
            }
            Assert.That(_report.cases[1].massBefore,Is.GreaterThan(_report.cases[0].massBefore));
            Assert.That(_report.cases[1].firstImpactDust,Is.GreaterThan(_report.cases[0].firstImpactDust));
            Assert.That(_report.cases[0].meshMask|_report.cases[1].meshMask,Is.EqualTo(15));
            foreach(var ps in new[]{dust,chips,fracture}) if(ps!=null) ps.Clear();
            for(int i=0;i<4;i++)
            {
                var gallery=New("Actual selected chip mesh "+i); gallery.layer=Layer;
                gallery.transform.SetPositionAndRotation(contact+right*((i-1.5f)*.9f)+up*.6f,Quaternion.LookRotation(forward,up)*Quaternion.Euler(15,i*19,9));
                gallery.transform.localScale=Vector3.one*.7f; gallery.AddComponent<MeshFilter>().sharedMesh=meshes[i]; gallery.AddComponent<MeshRenderer>().sharedMaterial=pool.StoneMaterial;
            }
            Capture(camera,"gallery-four-actual-chip-silhouettes");
            _report.stage="complete"; _report.passed=true;
#endif
        }
        private GameObject New(string name)
        { var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,_scene); return go; }
        private T[] All<T>() where T:Component => _scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static void SetRenderers(ParticleSystem a,ParticleSystem b,ParticleSystem c,bool enabled)
        { foreach(var ps in new[]{a,b,c}) if(ps!=null) ps.GetComponent<ParticleSystemRenderer>().enabled=enabled; }
        private static Color32[] Capture(UnityEngine.Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32); var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                var request=new RenderPipeline.StandardRequest { destination=target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True); RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(false,false);
                File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG()); return image.GetPixels32();
            }
            finally { RenderTexture.active=previous; UnityEngine.Object.Destroy(image); target.Release(); UnityEngine.Object.Destroy(target); }
        }
    }
}
