using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [Serializable] private sealed class FireVisualFrame
        {
            public string file, actor, backend;
            public string[] materials;
            public int frame, particles, triangles, drainingViews, volumeProxies, maximumRaySteps;
            public Bounds volumeBounds;
            public float time, length;
            public bool active, coverContact;
            public uint generation;
            public Vector3 muzzle, cameraPosition, cameraEuler, coverPoint;
        }
        [Serializable] private sealed class FireVisualCaptureReport
        {
            public string scope="Real saved EarthCoreSlice production fighters, production output camera/UI/post effects. Visual review required; nonempty/lifecycle checks do not prove flame quality.";
            public int width=1920,height=1080;
            public List<FireVisualFrame> frames=new List<FireVisualFrame>();
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator ProductionStoneFireFrontSideCoverAndReleaseAtNative1080()=>CaptureProductionFire(false);
        [UnityTest,Timeout(300000)]
        public IEnumerator CapsuleVolumePrototypeFrontSideOrbitInsideCoverAndReleaseAtNative1080()=>CaptureProductionFire(true);
        private IEnumerator CaptureProductionFire(bool volume)
        {
            string directory=volume?"BuildReports/HardPolish/G05/FireVisual-VolumePrototype":"BuildReports/HardPolish/G05/FireVisual";
            Directory.CreateDirectory(directory);
            var evidence=new FireVisualCaptureReport();
            var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;
            var output=new ProductionCaptureResolution();
            FireVisualCaptureCamera pose=null;
            GameObject cover=null;
            DirectFireInputLease visualInputLease=null;
            Vector3 originalPosition=camera.transform.position;
            Quaternion originalRotation=camera.transform.rotation;
            try
            {
                yield return output.WaitForRenderedSize(camera);
                yield return new WaitForEndOfFrame();
                ProductionCaptureResolution.SaveScreen(Path.Combine(directory,"columns-main.png"));
                yield return WaitSeconds(.28f);
                yield return new WaitForEndOfFrame();
                ProductionCaptureResolution.SaveScreen(Path.Combine(directory,"columns-main-motion.png"));
                if(volume)
                {
                    Shader volumeShader=null;
                    #if UNITY_EDITOR
                    volumeShader=UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Elemental/Presentation/Fire/Shaders/FireCapsuleVolume.shader");
                    #endif
                    Assert.That(volumeShader,Is.Not.Null,"QA requires an explicit shader reference; never fall back silently to ribbons.");
                    for(int i=0;i<8&&binding.Presenter(i)!=null;i++)binding.Presenter(i).CpuDiagnostics.EnableCapsuleVolumeForQa(volumeShader,40);
                    evidence.scope+=" EXPLICIT LOCAL VOLUME PROTOTYPE: capsule replaces ribbon and parcel renderers. Existing ribbon form tests do not validate this path. Includes orbit, camera-inside and opacity diagnostic.";
                }
                yield return EnterCombat();
                var actor=duel.PlayerTransform.GetComponentInChildren<HumanoidCharacterPresentation>();
                Assert.That(actor,Is.Not.Null);
                string[] materials=actor.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)
                    .Where(m=>m!=null).Select(m=>m.name+" ["+m.shader.name+"]").Distinct().ToArray();
                Assert.That(materials.Any(m=>m.Contains("Rumble Rock")||m.Contains("Stone")),Is.True,
                    "This fixture requires the saved production fighter and its existing stone accessories, not a diagnostic mannequin.");
                var input=duel.PlayerTransform.GetComponentInChildren<Elemental.Input.Gestures.MagicInputController>(true);
                Assert.That(input,Is.Not.Null);
                Assert.That(input.TrySelectElement(Elemental.Simulation.Magic.ElementId.Fire),Is.True);
                visualInputLease=new DirectFireInputLease(input);
                yield return new WaitForFixedUpdate();
                Assert.That(input.isActiveAndEnabled,Is.False);
                var session=binding.PlayerSession;
                Vector3 forward=duel.PlayerTransform.forward;
                Vector3 right=duel.PlayerTransform.right;
                Vector3 focus=session.MuzzlePosition+forward*7+Vector3.up*1.0f;
                pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
                Vector3 subject=duel.PlayerTransform.position+Vector3.up*1.25f+forward*1.8f;
                Vector3 frontSubject=duel.PlayerTransform.position+Vector3.up*1.25f+forward*.6f;
                pose.Place(FindFireFrontView(duel.PlayerTransform,session.MuzzlePosition,frontSubject,forward,right),frontSubject);
                Assert.That(session.TryBegin(focus),Is.True);
                int slot=session.Group.Slot;
                yield return WaitSeconds(.3f);
                Assert.That(session.IsActive,Is.True);
                if(volume)
                {
                    Assert.That(binding.Presenter(slot).CpuDiagnostics.ActiveCapsuleVolume,Is.True);
                    Assert.That(binding.Presenter(slot).CpuDiagnostics.BodyDiagnostics.Visible,Is.False,"Volume must replace the old capsule ribbons.");
                    Assert.That(binding.Presenter(slot).AliveParticles,Is.Zero,"No parcel sheets may hide the volume silhouette.");
                }
                else if(binding.Presenter(slot).CpuDiagnostics.FlowDiagnostics!=null)
                    Assert.That(binding.Presenter(slot).CpuDiagnostics.FlowDiagnostics.Visible,Is.True);
                else Assert.That(binding.Presenter(slot).CpuDiagnostics.BodyDiagnostics.Visible,Is.True);
                yield return CaptureFireFrame("front-hold-030",camera,actor,materials,session,slot,directory,evidence);
                yield return WaitSeconds(.35f);
                yield return CaptureFireFrame("front-hold-065",camera,actor,materials,session,slot,directory,evidence);
                pose.Place(subject+right*7.6f+Vector3.up*1.3f,subject);
                yield return WaitSeconds(.15f);
                yield return CaptureFireFrame("side-hold-080",camera,actor,materials,session,slot,directory,evidence);
                yield return WaitSeconds(.3f);
                yield return CaptureFireFrame("side-hold-110",camera,actor,materials,session,slot,directory,evidence);
                Vector3 freeDirection=FindClearFireDirection(duel.PlayerTransform,session.MuzzlePosition);
                focus=session.MuzzlePosition+freeDirection*FireStreamSession.Range;
                session.SetAim(focus);
                Vector3 freeSubject=duel.PlayerTransform.position+Vector3.up+freeDirection*3;
                Vector3 freeSide=Vector3.Cross(Vector3.up,freeDirection).normalized;
                pose.Place(freeSubject+freeSide*10+Vector3.up,freeSubject);
                yield return WaitSeconds(.25f);
                Assert.That(session.CurrentLength,Is.GreaterThan(7),"The free-stream view must show full range, not the earlier opponent-clipped trace.");
                yield return CaptureFireFrame("free-stream-side",camera,actor,materials,session,slot,directory,evidence);
                if(volume)
                {
                    // Actual advancing-time frames: record timestamps, not a claim
                    // that PNG encoding holds a fixed cadence.
                    for(int frame=0;frame<8;frame++)
                    {
                        yield return WaitSeconds(.06f);
                        yield return CaptureFireFrame("volume-motion-"+frame,camera,actor,materials,session,slot,directory,evidence);
                    }
                    var volumeBackend=binding.Presenter(slot).CpuDiagnostics.VolumeDiagnostics;
                    float beforeComparisonScale=Time.timeScale;
                    try
                    {
                        Time.timeScale=0;volumeBackend.SetSnapshotFrozenForQa(true);
                        Bounds fixedBounds=volumeBackend.WorldBounds;
                        foreach(int samples in new[]{32,40,48})
                        {
                            volumeBackend.SetRayStepsForQa(samples);
                            yield return CaptureFireFrame("volume-frozen-"+samples,camera,actor,materials,session,slot,directory,evidence);
                            Assert.That(volumeBackend.WorldBounds,Is.EqualTo(fixedBounds));
                        }
                    }
                    finally{volumeBackend.SetRayStepsForQa(40);volumeBackend.SetSnapshotFrozenForQa(false);Time.timeScale=beforeComparisonScale;}
                    for(int frame=0;frame<4;frame++)
                    {
                        Vector3 around=Quaternion.AngleAxis(frame*30,Vector3.up)*freeSide;
                        pose.Place(freeSubject+around*8+Vector3.up,freeSubject);
                        yield return WaitSeconds(.12f);
                        yield return CaptureFireFrame("volume-orbit-"+frame,camera,actor,materials,session,slot,directory,evidence);
                    }
                    Vector3 inside=session.MuzzlePosition+freeDirection*2;
                    pose.Place(inside,inside+freeDirection);
                    yield return WaitSeconds(.08f);
                    yield return CaptureFireFrame("volume-camera-inside",camera,actor,materials,session,slot,directory,evidence);
                    binding.Presenter(slot).CpuDiagnostics.VolumeDiagnostics.SetDebugViewForQa(1);
                    yield return CaptureFireFrame("volume-inside-opacity",camera,actor,materials,session,slot,directory,evidence);
                    binding.Presenter(slot).CpuDiagnostics.VolumeDiagnostics.SetDebugViewForQa(0);
                    pose.Place(freeSubject+freeSide*10+Vector3.up,freeSubject);
                }
                Vector3 direction=(focus-session.MuzzlePosition).normalized;
                cover=GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name="Fire capture owned finite stone cover";
                cover.transform.SetPositionAndRotation(session.MuzzlePosition+direction*2.8f,Quaternion.LookRotation(direction));
                cover.transform.localScale=new Vector3(2.5f,2.7f,.35f);
                // Use an existing stone material; this test adds only its explicit finite box surface.
                Material stone=actor.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)
                    .First(m=>m!=null&&(m.shader.name.Contains("Rumble Rock")||m.name.Contains("Stone")));
                cover.GetComponent<Renderer>().sharedMaterial=stone;
                cover.AddComponent<FireSurfaceBinding>().Configure(0x7f001905);
                Physics.SyncTransforms();
                yield return WaitSeconds(.25f);
                Assert.That(session.HasCoverContact,Is.True,"Cover frame must contain an actual authority contact.");
                Assert.That(session.CurrentLength,Is.LessThan(3.5f));
                yield return CaptureFireFrame("side-cover-contact",camera,actor,materials,session,slot,directory,evidence);
                session.Stop();
                yield return CaptureFireFrame("release-000",camera,actor,materials,session,slot,directory,evidence);
                Assert.That(session.IsActive,Is.False);
                Assert.That(binding.Presenter(slot).AliveParticles,Is.GreaterThan(0),"Release must retain the admitted tail.");
                yield return WaitSeconds(.15f);
                yield return CaptureFireFrame("release-015",camera,actor,materials,session,slot,directory,evidence);
                yield return WaitSeconds(.25f);
                yield return CaptureFireFrame("release-040",camera,actor,materials,session,slot,directory,evidence);
                yield return WaitSeconds(.8f);
                yield return CaptureFireFrame("release-120",camera,actor,materials,session,slot,directory,evidence);
                Assert.That(binding.DrainingViews,Is.Zero);
                Assert.That(binding.Presenter(slot).AliveParticles,Is.Zero);
            }
            finally
            {
                if(binding!=null&&binding.PlayerSession!=null)binding.PlayerSession.Stop();
                visualInputLease?.Dispose();
                if(pose!=null){pose.enabled=false;UnityEngine.Object.Destroy(pose);}
                if(cover!=null)UnityEngine.Object.Destroy(cover);
                if(camera!=null)camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
                output.Dispose();
                File.WriteAllText(Path.Combine(directory,"Capture.json"),JsonUtility.ToJson(evidence,true));
            }
        }
        private static Vector3 FindClearFireDirection(Transform actor,Vector3 origin)
        {
            foreach(float elevation in new[]{.5f,1f,1.7f})foreach(float yaw in new[]{0f,45f,-45f,90f,-90f,180f})
            {
                Vector3 direction=(Quaternion.AngleAxis(yaw,Vector3.up)*actor.forward+Vector3.up*elevation).normalized;
                var hits=Physics.SphereCastAll(origin,FireStreamSession.Radius+.06f,direction,FireStreamSession.Range,~0,QueryTriggerInteraction.Ignore);
                if(hits.All(hit=>hit.transform.IsChildOf(actor)))return direction;
            }
            Assert.Fail("No clear finite full-range fire direction in the saved arena.");return Vector3.up;
        }
        private static Vector3 FindFireFrontView(Transform actor,Vector3 muzzle,Vector3 subject,Vector3 forward,Vector3 right)
        {
            // Finite camera-to-subject tests; no scenery renderer is hidden to get a shot.
            var hits=new RaycastHit[64];
            float[] distances={3.3f,4.1f,4.8f};
            float[] angles={45,-45,65,-65,25,-25};
            Vector3[] targets={actor.position+Vector3.up*1.35f,muzzle,actor.position+Vector3.up*.75f};
            Physics.SyncTransforms();
            foreach(float distance in distances)foreach(float degrees in angles)
            {
                float angle=degrees*Mathf.Deg2Rad;
                Vector3 candidate=subject+(forward*Mathf.Cos(angle)+right*Mathf.Sin(angle))*distance+Vector3.up*.65f;
                bool clear=true;
                foreach(Vector3 target in targets)
                {
                    Vector3 ray=target-candidate;
                    int count=Physics.RaycastNonAlloc(candidate,ray.normalized,hits,ray.magnitude-.03f,~0,QueryTriggerInteraction.Ignore);
                    if(count==hits.Length){clear=false;break;}
                    for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(actor)){clear=false;break;}
                    if(!clear)break;
                }
                if(clear)return candidate;
            }
            Assert.Fail("No finite unoccluded front camera candidate sees the actual fighter chest, head and muzzle. Keep scenery visible and inspect scene geometry.");
            return subject;
        }
        private IEnumerator CaptureFireFrame(string label,Camera camera,HumanoidCharacterPresentation actor,
            string[] materials,FireStreamSession session,int slot,string directory,FireVisualCaptureReport evidence)
        {
            yield return new WaitForEndOfFrame();
            string file=label+".png";
            ProductionCaptureResolution.SaveScreen(Path.Combine(directory,file));
            SchoolHintCaptureDiagnostics.Write(Path.Combine(directory,file+"-hud.txt"));
            var view=binding.Presenter(slot);
            evidence.frames.Add(new FireVisualFrame {file=file,actor=actor.name,materials=materials,
                backend=view.CpuDiagnostics.FlowDiagnostics!=null?"CollisionDrivenParcelVolume":view.CpuDiagnostics.VolumeDiagnostics!=null?"LocalCapsuleVolumePrototype":view.ActiveBackend.ToString(),frame=Time.frameCount,time=Time.time,particles=view.AliveParticles,
                triangles=view.CpuDiagnostics.FlowDiagnostics!=null?view.CpuDiagnostics.FlowDiagnostics.ProxyTriangles:view.CpuDiagnostics.VolumeDiagnostics!=null?view.CpuDiagnostics.VolumeDiagnostics.ProxyTriangles:view.CpuDiagnostics.BodyDiagnostics.ActiveTriangles,drainingViews=binding.DrainingViews,
                volumeProxies=view.CpuDiagnostics.VolumeDiagnostics?.ActiveVolumes??0,maximumRaySteps=view.CpuDiagnostics.VolumeDiagnostics?.MaximumRaySteps??0,
                volumeBounds=view.CpuDiagnostics.VolumeDiagnostics?.WorldBounds??default,
                active=session.IsActive,generation=session.Generation,length=session.CurrentLength,
                coverContact=session.HasCoverContact,coverPoint=session.CoverPoint,muzzle=session.MuzzlePosition,
                cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles});
        }
    }
    // The fixture owns only the final camera transform. Existing pipeline, subject,
    // lighting, post effects and saved actor remain in their production scene.
    [DefaultExecutionOrder(32000)]
    public sealed class FireVisualCaptureCamera : MonoBehaviour
    {
        private Vector3 position;
        private Quaternion rotation;
        public void Place(Vector3 at,Vector3 subject){position=at;rotation=Quaternion.LookRotation(subject-at,Vector3.up);}
        private void LateUpdate()=>transform.SetPositionAndRotation(position,rotation);
    }
}
