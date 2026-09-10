using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [Serializable]private sealed class NaturalDuskSample{public float time,phase;public Color sky,equator;}
        [Serializable]private sealed class NaturalDuskReport{public List<NaturalDuskSample> frames=new();}
        [UnityTest,Timeout(240000)]public IEnumerator NaturalClockSunsetHasNoBlueAmbientOvershootOrShaderSwap()
        {
            var sky=_scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single();
            var profile=(CelestialSystemProfile)typeof(CelestialSystemBehaviour).GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sky);
            var actor=ShortPlayer();var materials=actor.Presentation.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
            var shaders=materials.Select(m=>m.shader).ToArray();var report=new NaturalDuskReport();
            string folder="BuildReports/HardPolish/G02/NaturalSunset-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
            float oldPhase=sky.Snapshot.TimeOfDay01,oldScale=Time.timeScale;var camera=sky.TargetCamera;var pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
            try
            {
                actor.Input.Move=Unity.Mathematics.float2.zero;
                pose.Place(actor.Presentation.transform.position+actor.Presentation.transform.forward*5+Vector3.up*1.8f,actor.Presentation.transform.position+Vector3.up*.7f);
                Time.timeScale=1;sky.SetTimeOfDayForQa(.48f);sky.EvaluatePresentationForQa();
                float start=Time.time,next=start;int capture=0;double deadline=Time.realtimeSinceStartupAsDouble+90;
                while(sky.Snapshot.TimeOfDay01<.53f&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return new WaitForEndOfFrame();
                    Assert.That(RenderSettings.ambientSkyColor.b,Is.LessThanOrEqualTo(Mathf.Max(profile.DayAmbientSky.b,profile.NightAmbient.b*1.55f)+.001f),"Transient sky-blue amplification");
                    Assert.That(RenderSettings.ambientEquatorColor.b,Is.LessThanOrEqualTo(Mathf.Max(profile.DayAmbientEquator.b,profile.NightAmbient.b*1.05f)+.001f),"Equatorial light was replaced by blue sky fill");
                    for(int i=0;i<materials.Length;i++)Assert.That(materials[i].shader,Is.SameAs(shaders[i]));
                    if(Time.time>=next){next=Time.time+1.5f;report.frames.Add(new NaturalDuskSample{time=Time.time-start,phase=sky.Snapshot.TimeOfDay01,sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor});ProductionCaptureResolution.SaveScreen(folder+"/sunset-"+(capture++).ToString("D3")+".png");}
                }
                Assert.That(sky.Snapshot.TimeOfDay01,Is.GreaterThanOrEqualTo(.53f),"The production clock did not advance through dusk");
                Assert.That(report.frames.Count,Is.GreaterThan(10));
            }
            finally{Time.timeScale=oldScale;sky.SetTimeOfDayForQa(oldPhase);sky.EvaluatePresentationForQa();UnityEngine.Object.Destroy(pose);File.WriteAllText(folder+"/frames.json",JsonUtility.ToJson(report,true));}
        }
    }
}
