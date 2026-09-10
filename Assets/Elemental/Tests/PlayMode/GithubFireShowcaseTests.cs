using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireWeaving()=>RecordFireShowcase("fire-weaving");
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireProjectiles()=>RecordFireShowcase("fire-projectiles");
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireOrbitAttacks()=>RecordFireShowcase("fire-orbit-attacks");
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireContour()=>RecordFireShowcase("fire-contour");
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireRing()=>RecordFireShowcase("fire-ring");
  [UnityTest,Timeout(240000)] public IEnumerator GithubFireFlight()=>RecordFireShowcase("fire-flight");
  [UnityTest,Timeout(240000)] public IEnumerator GithubElementalDuel()=>RecordFireShowcase("elemental-duel");

  // Choreography calls the same admitted runtime abilities as device input. It does
  // not inject render-only cues, disable collision/damage or claim network footage.
  private IEnumerator RecordFireShowcase(string clip)
  {
   float oldStep=Time.captureDeltaTime;IDisposable rival=null;FireVisualCaptureCamera cameraRig=null;
   bool passed=false;
   string folder="BuildReports/Showcase/"+clip+(clip=="elemental-duel"?"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"):"");Directory.CreateDirectory(folder);
   var proof=new System.Text.StringBuilder("Scripted in-engine capture; real saved arena, runtime effects and physics. 12 fps.\n");
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
    bool battle=clip=="elemental-duel";
    if(!battle)rival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    else foreach(var bot in All<Elemental.Runtime.Characters.EarthMvpBotController>())bot.enabled=true;
    var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;
    cameraRig=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
    Vector3 start=flightMotor.Body.position,up=flightMotor.LocalUp,forward=flightMotor.FacingForward;
    Vector3 side=Vector3.Cross(up,forward).normalized;
    Vector3 aim=start+forward*16+up*2;
    int frame=0,casts=0,sources=0;float duration=battle?7:clip=="fire-weaving"?8:5;
    int count=Mathf.RoundToInt(duration*60);
    for(int tick=0;tick<count;tick++)
    {
     if(battle&&(duel.PlayerHealth<=0||duel.BotHealth<=0||!flightAbility.IsAvailable))
     {proof.AppendLine("Stopped at combat end before corpse/round-transition footage; tick="+tick);break;}
     float t=tick/60f;Vector3 root=flightMotor.Body.position;
     if(battle)aim=duel.BotTransform.position+up*.8f;
     if(clip=="fire-contour"&&tick>=135)aim=flightMotor.SupportFeetPoint(up)+forward*1.4f;
     flightAbility.SetChargedAim(aim);flightMotor.SetAimDirection(aim-root);
     switch(clip)
     {
      case "fire-weaving":
       var form=t<1.5f?FireWeaveForm.Jet:t<3?FireWeaveForm.Flood:t<4.5f?FireWeaveForm.Orbit:FireWeaveForm.Sphere;
       float power=Mathf.Clamp01(t/5.8f);flightAbility.SetWeaveHeld(t<=6,form,power);
       if(t<3){binding.PlayerSession.SetPower(FireWeaveTuning.StreamPower(power));if(!binding.PlayerSession.IsActive)Assert.That(binding.PlayerSession.TryBegin(aim),Is.True);binding.PlayerSession.SetAim(aim);}
       else binding.PlayerSession.Stop();
       if(tick==360){Assert.That(flightAbility.TryReleaseSphereWave(),Is.True);casts++;}
       break;
      case "fire-projectiles":
       if(tick==12||tick==100){Assert.That(flightAbility.BeginBoltCharge(aim),Is.True);casts++;}
       if(tick==23||tick==184)Assert.That(flightAbility.ReleaseBoltCharge(),Is.True);
       break;
      case "fire-orbit-attacks":
       flightAbility.SetWeaveHeld(true,FireWeaveForm.Orbit,.65f);
       if(tick==50||tick==75||tick==100){Assert.That(flightAbility.TryRapidShot(aim),Is.True);casts++;}
       if(tick==160){Assert.That(flightAbility.TryDrill(aim),Is.True);casts++;}
       break;
      case "fire-contour":
       if(tick==0)flightAbility.BeginContour();
       if(tick<130&&tick%6==0){float x=tick/130f;Vector3 point=start+forward*(2+x*4)+side*(Mathf.Sin(x*Mathf.PI*2)*1.4f);if(flightAbility.TraceContour(point))sources++;}
       if(tick==135){flightAbility.BeginContour();Assert.That(flightAbility.TraceContour(aim),Is.True,"Fresh supported source must be admitted before the reaction.");sources++;}
       if(tick==138){flightAbility.SetWeaveHeld(true,FireWeaveForm.Jet,.25f);binding.PlayerSession.SetPower(FireWeaveTuning.StreamPower(.25f));Assert.That(binding.PlayerSession.TryBegin(aim),Is.True);casts++;}
       if(tick>=138&&tick<195)binding.PlayerSession.SetAim(aim);
       if(tick==195){binding.PlayerSession.Stop();flightAbility.SetWeaveHeld(false,FireWeaveForm.Jet,.25f);}
       break;
      case "fire-ring":
       if(tick==10){Assert.That(flightAbility.TryRing(),Is.True);casts++;}
       if(tick==120)flightAbility.ReleaseRing();
       break;
      case "fire-flight":
       flightAbility.SetLiftHeld(tick<115);if(tick==0)casts++;
       break;
      case "elemental-duel":
       // Real defense begins before the opponent's first volley. The player
       // remains vulnerable; admission and the actual collision shield decide.
       flightAbility.SetWeaveHeld(true,tick<35||tick>=230?FireWeaveForm.Sphere:FireWeaveForm.Orbit,tick<35||tick>=230?1:.55f);
       if((tick==42||tick==76)&&flightAbility.TryRapidShot(aim)){casts++;proof.AppendLine("Rapid shot at "+t);}
       if(tick==112&&flightAbility.BeginBoltCharge(aim))proof.AppendLine("Bolt charge admitted at "+t);
       if(tick==149&&flightAbility.ReleaseBoltCharge()){casts++;proof.AppendLine("Bolt released at "+t);}
       if(tick==180&&flightAbility.TryRing())proof.AppendLine("Ring charge admitted at "+t);
       if(tick==218&&flightAbility.IsRingCharging){flightAbility.ReleaseRing();casts++;proof.AppendLine("Ring released at "+t);}
       if(tick==300&&flightAbility.TryReleaseSphereWave()){casts++;proof.AppendLine("Defensive sphere wave at "+t);}
       break;
     }
     Vector3 subject=clip=="fire-flight"?root+up:root+up*1.1f;
     Vector3 at=subject-forward*7+side*5+up*3;
     if(clip=="fire-contour"){subject=start+forward*3;at=subject-forward*6+side*5+up*7;}
     if(clip=="fire-flight"){subject=root+up*.4f;at=root+side*4-forward*2+up*.9f;}
     if(clip=="fire-ring"){subject=root;at=root-forward*10+side*3+up*9;}
     if(clip=="fire-projectiles"||clip=="fire-orbit-attacks"){subject=root+forward*3+up;at=subject+side*9-forward*3+up*3;}
     if(battle)
     {
      Vector3 separation=Vector3.ProjectOnPlane(duel.BotTransform.position-root,up);
      Vector3 duelAxis=separation.sqrMagnitude>.01f?separation.normalized:forward;
      Vector3 viewSide=Vector3.Cross(up,duelAxis).normalized;
      subject=(root+duel.BotTransform.position)*.5f+up;
      float distance=Mathf.Clamp(separation.magnitude*.8f+4,8,14);
      at=subject+viewSide*distance-duelAxis*2+up*4;
     }
     cameraRig.Place(at,subject);
     yield return new WaitForEndOfFrame();
     if(tick%5==0){var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/frame"+(frame++).ToString("D4")+".jpg",image.EncodeToJPG(90));}finally{UnityEngine.Object.Destroy(image);}}
    }
    proof.AppendLine("observed casts="+casts+" contourAnchors="+sources+" amplifications="+flightAbility.Amplifications);
    if(clip=="fire-contour")Assert.That(sources,Is.GreaterThan(3),"Contour must have admitted supported anchors.");
    else Assert.That(casts,Is.GreaterThan(0));
    if(clip=="fire-contour")Assert.That(flightAbility.Amplifications,Is.GreaterThan(0),"The real stream must actually react with the fresh drawn fire source.");
    if(battle)
    {
     Assert.That(casts,Is.GreaterThanOrEqualTo(2),"Hero duel must contain at least two successfully released Fire attacks.");
     int strikes=All<Elemental.Runtime.Characters.EarthMvpBotController>().Sum(x=>x.StrikeCount);
     Assert.That(strikes,Is.GreaterThan(0),"Duel footage must include actual opponent attacks.");
     proof.AppendLine("actualEarthStrikes="+strikes+" playerHealth="+duel.PlayerHealth+" botHealth="+duel.BotHealth);
    }
    proof.AppendLine("frames="+frame+" admittedCasts="+casts+" contourAnchors="+sources+" amplifications="+flightAbility.Amplifications);
    passed=true;
   }
   finally{proof.AppendLine("passed="+passed);File.WriteAllText(folder+"/capture.txt",proof.ToString());binding?.PlayerSession?.Stop();rival?.Dispose();Time.captureDeltaTime=oldStep;if(cameraRig!=null)UnityEngine.Object.Destroy(cameraRig);ReleaseFireAbilityFixture();}
  }
 }
}
