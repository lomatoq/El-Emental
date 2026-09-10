using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)]
  public IEnumerator SavedFighterMeteorSheathFollowsPosedBodyDuringUnobstructedFlight()=>MeteorBodyAttachment(false);
  [UnityTest,Timeout(240000)]
  public IEnumerator ShowcaseActualMeteorFlightAtTwelveFramesPerSecond()=>MeteorBodyAttachment(true);
  private IEnumerator MeteorBodyAttachment(bool showcase)
  {
   FireFlightMotionInput input=null;MonoBehaviour original=null;System.IDisposable rival=null;
   FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;Transform oldFrame=null;GameObject frame=null;
   Vector3 originalPosition=default,originalVelocity=default,originalAngular=default;Quaternion originalRotation=default;bool relocated=false;
   FireWorldImpact worldImpact=null;System.Action<FireSurfaceContact> heatObserver=null;
   var contacts=new System.Collections.Generic.List<string>();
   var motion=new System.Collections.Generic.List<string>{"frame,time,fixedTime,timeScale,speed,velocity,position,clearance,blocked,move,stun,grounded,bodyDamping,bodyMaxSpeed"};
   var probes=new System.Collections.Generic.List<FireLowFlightContactRecorder>();
   var cameraFrameField=typeof(PlanetMotor).GetField("cameraFrame",BindingFlags.NonPublic|BindingFlags.Instance);
   string folder="BuildReports/HardPolish/G05/MeteorBody-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
   const string showcaseFolder="BuildReports/Showcase/fire-meteor";
   int showcaseFrame=0,showcaseTick=0;
   try
   {
    if(showcase)
    {
     Directory.CreateDirectory(showcaseFolder);
     // Only this fixture's named frames; stale frames must not extend the next GIF.
     foreach(string old in Directory.GetFiles(showcaseFolder,"frame????.jpg"))File.Delete(old);
    }
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
    rival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    original=(MonoBehaviour)typeof(PlanetMotor).GetField("inputSourceBehaviour",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flightMotor);
    input=flightMotor.gameObject.AddComponent<FireFlightMotionInput>();flightMotor.ConfigureInputSource(input);
    Directory.CreateDirectory(folder);
    originalPosition=flightMotor.Body.position;originalRotation=flightMotor.Body.rotation;
    originalVelocity=flightMotor.Body.linearVelocity;originalAngular=flightMotor.Body.angularVelocity;
    var debris=All<EarthRockDebris>();
    foreach(var piece in debris)if(piece.gameObject.activeInHierarchy)contacts.Add("before settle "+piece.name+" pos="+piece.transform.position.ToString("F3")+" velocity="+piece.Body.linearVelocity.ToString("F3"));
    worldImpact=binding.PlayerSession.GetComponent<FireWorldImpact>();
    heatObserver=hit=>{if(contacts.Count<1200)contacts.Add(Time.fixedTime.ToString("F5")+" actual heat receiver="+hit.Surface.name+" point="+hit.Point.ToString("F4")+" energy="+hit.Energy);};
    worldImpact.ContactAccepted+=heatObserver;
    // Let the actual startup rocks fall/settle. No collider, velocity or damage override.
    int quietTicks=0;
    for(int tick=0;tick<90;tick++)
    {
     yield return new WaitForFixedUpdate();bool moving=false;
     foreach(var piece in debris)if(piece.gameObject.activeInHierarchy&&piece.Body!=null&&!piece.Body.isKinematic&&Vector3.Distance(piece.Body.position,flightMotor.Body.position)<10&&piece.Body.linearVelocity.sqrMagnitude>1.44f){moving=true;break;}
     quietTicks=moving?0:quietTicks+1;if(tick>=30&&quietTicks>=10)break;
    }
    foreach(var piece in debris)if(piece.gameObject.activeInHierarchy)contacts.Add("after settle "+piece.name+" pos="+piece.transform.position.ToString("F3")+" velocity="+piece.Body.linearVelocity.ToString("F3"));
    Vector3 up=flightMotor.LocalUp,feet=flightMotor.SupportFeetPoint(up),direction=flightMotor.FacingForward;
    Vector3 right=Vector3.Cross(up,direction).normalized,baseForward=direction;Vector3 selectedFeet=feet;bool found=false;
    oldFrame=(Transform)cameraFrameField.GetValue(flightMotor);
    frame=new GameObject("Fixed world movement frame, separate from capture camera");cameraFrameField.SetValue(flightMotor,frame.transform);
    var probeHits=new RaycastHit[64];var occupied=new Collider[64];var routeNotes=new System.Collections.Generic.List<string>();
    float bodyRadius=flightMotor.Capsule.radius*Mathf.Max(Mathf.Abs(flightMotor.transform.lossyScale.x),Mathf.Abs(flightMotor.transform.lossyScale.z));
    float corridorRadius=Mathf.Max(.72f,bodyRadius+.3f);int checkedLanes=0;
    // Candidate starts are all actual raycast-supported arena locations. Search a
    // wider path than the body alone so adjacent rocks cannot enter from its wake.
    for(int startIndex=0;startIndex<25&&!found;startIndex++)
    {
     int ring=startIndex==0?0:(startIndex-1)/8+1;float angle=(startIndex==0?0:(startIndex-1)%8)*45*Mathf.Deg2Rad;
     Vector3 probe=feet+(right*Mathf.Cos(angle)+baseForward*Mathf.Sin(angle))*(ring*2);
     if(!Physics.Raycast(probe+up*3,-up,out var initial,6,flightMotor.GroundMask,QueryTriggerInteraction.Ignore)||Vector3.Dot(initial.normal,up)<.75f)continue;
     // Native109 selected the top of Debris05 as ground; sweeps alone do not
     // report initial overlap. This visual proof starts on the actual authored floor.
     if(initial.collider.name!="Arena_FloorBase_INTACT")continue;
     float standingHeight=Mathf.Max(bodyRadius*2,flightMotor.Capsule.height*Mathf.Abs(flightMotor.transform.lossyScale.y));
     int overlapCount=Physics.OverlapCapsuleNonAlloc(initial.point+up*(bodyRadius+.06f),initial.point+up*(standingHeight-bodyRadius+.06f),bodyRadius+.04f,occupied,~0,QueryTriggerInteraction.Ignore);
     bool clearStart=overlapCount<occupied.Length;
     for(int k=0;k<overlapCount&&clearStart;k++)if(occupied[k]!=initial.collider&&!occupied[k].transform.IsChildOf(flightMotor.transform))clearStart=false;
     if(!clearStart)continue;
     for(int heading=0;heading<24&&!found;heading++)
     {
      checkedLanes++;Vector3 candidate=Quaternion.AngleAxis(heading*15,up)*baseForward;bool clear=true;
      for(int level=0;level<3&&clear;level++)
      {
       Vector3 elevated=initial.point+up*(corridorRadius+.18f+level*.6f);
       int count=Physics.SphereCastNonAlloc(elevated,corridorRadius,candidate,probeHits,7.5f,~0,QueryTriggerInteraction.Ignore);
       if(count==probeHits.Length){clear=false;break;}
       for(int k=0;k<count;k++)
       {
        var contact=probeHits[k];if(contact.collider==null||contact.collider.attachedRigidbody==flightMotor.Body||contact.collider.transform.IsChildOf(flightMotor.transform))continue;
        if(contact.distance<7.3f){clear=false;break;}
       }
      }
      for(int step=0;step<=7&&clear;step++)
       if(!Physics.Raycast(initial.point+candidate*step+up*1.5f,-up,out var support,3,flightMotor.GroundMask,QueryTriggerInteraction.Ignore)||Vector3.Dot(support.normal,up)<.65f)clear=false;
      // Sweeps see the present state; reject nearby moving debris whose one-second
      // ballistic envelope can intersect this selected corridor after takeoff.
      foreach(var piece in debris)
      {
       if(!clear||!piece.gameObject.activeInHierarchy||piece.Body==null||piece.Body.isKinematic||piece.Body.linearVelocity.sqrMagnitude<1.44f)continue;
       for(int sample=0;sample<3;sample++)
       {
        Vector3 moving=piece.Body.position+piece.Body.linearVelocity*(sample*.5f);
        Vector3 near=initial.point+candidate*Mathf.Clamp(Vector3.Dot(moving-initial.point,candidate),0,7.5f)+up*1.2f;
        if(Vector3.Distance(moving,near)<corridorRadius+piece.BreakRadius+.4f){clear=false;break;}
       }
      }
      if(clear){found=true;selectedFeet=initial.point;direction=candidate;}
     }
    }
    routeNotes.Add("checkedLanes="+checkedLanes+" corridorRadius="+corridorRadius+" quietTicks="+quietTicks+" selectedSupport=Arena_FloorBase_INTACT selectedFeet="+selectedFeet.ToString("F4")+" direction="+direction.ToString("F4"));
    File.WriteAllLines(folder+"/lane-selection.txt",routeNotes);
    Assert.That(found,Is.True,"Need a genuinely clear supported route on the real arena after startup settles.");
    // One authoritative setup relocation. Subsequent travel is entirely the real motor.
    flightMotor.Body.position+=selectedFeet+up*.035f-flightMotor.SupportFeetPoint(up);
    flightMotor.Body.linearVelocity=Vector3.zero;flightMotor.Body.angularVelocity=Vector3.zero;flightMotor.ResetAfterTeleport();relocated=true;Physics.SyncTransforms();
    for(int settle=0;settle<5;settle++)yield return new WaitForFixedUpdate();
    up=flightMotor.LocalUp;direction=Vector3.ProjectOnPlane(direction,up).normalized;
    frame.transform.rotation=Quaternion.LookRotation(direction,up);flightMotor.SetAimDirection(direction);input.Move=new float2(0,1);
    var fx=binding.GetComponent<FireAbilityEffects>();
    var bow=(FireMeteorBowRenderer)typeof(FireAbilityEffects).GetField("meteorBow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(fx);
    var head=(Transform)typeof(FireMeteorBowRenderer).GetField("head",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bow);
    var hips=(Transform)typeof(FireMeteorBowRenderer).GetField("hips",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bow);
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();Directory.CreateDirectory(folder);
    foreach(var bodyPart in flightMotor.GetComponentsInChildren<Rigidbody>(true))
    {
     var probe=bodyPart.gameObject.AddComponent<FireLowFlightContactRecorder>();probe.Records=contacts;probes.Add(probe);
     contacts.Add("body="+bodyPart.name+" kinematic="+bodyPart.isKinematic+" mass="+bodyPart.mass+" collide="+bodyPart.detectCollisions);
    }
    flightAbility.SetLowFlightHeld(true);float peakSpeed=0,peakError=0;double peakMs=0;
    int freeTravelFrames=0,contactFrames=0,captures=0;Vector3 began=flightMotor.Body.position;
    // This fixture proves attachment during real travel. The separate wall fixture
    // owns collision response; a brief contact with authored relief is not a visual failure.
    for(int i=0;i<70&&freeTravelFrames<18;i++)
    {
     Vector3 body=(head.position+hips.position)*.5f;
     int cameraAngle=Mathf.Min(2,freeTravelFrames/6);Vector3 cameraSide=Vector3.Cross(up,direction).normalized;
     Vector3 cameraOffset=(showcase||cameraAngle==0)?-direction*5+up*2+cameraSide*.65f:cameraAngle==1?cameraSide*4.5f+up*1.4f:direction*4.5f+up*1.7f+cameraSide*.6f;
     capture.Place(body+cameraOffset,body+direction*.15f);
     yield return new WaitForEndOfFrame();
     if(showcase&&showcaseTick++%5==0)SaveMeteorShowcaseFrame(showcaseFolder,showcaseFrame++);
     Assert.That(flightMotor.FireLowFlightActive,Is.True);
     if(flightMotor.FireLowFlightBlocked)
     {
      contactFrames++;
      string obstacle=flightMotor.Body.SweepTest(flightMotor.FireLowFlightDirection,out var blocked,.5f,QueryTriggerInteraction.Ignore)?blocked.collider.name+" at "+blocked.distance:"no persistent collider";
      File.AppendAllText(folder+"/lane-selection.txt","\nblocked frame="+i+" by="+obstacle+" travelDirection="+flightMotor.FireLowFlightDirection+" expected="+direction+" groundedClearance="+flightMotor.FireLowFlightClearance);
     }

     float currentSpeed=Vector3.ProjectOnPlane(flightMotor.Body.linearVelocity,flightMotor.LocalUp).magnitude;
     peakSpeed=Mathf.Max(peakSpeed,currentSpeed);
     motion.Add(i+","+Time.time.ToString("F5")+","+Time.fixedTime.ToString("F5")+","+Time.timeScale+","+currentSpeed.ToString("F5")+",\""+flightMotor.Body.linearVelocity.ToString("F5")+"\",\""+flightMotor.Body.position.ToString("F5")+"\","+flightMotor.FireLowFlightClearance.ToString("F5")+","+flightMotor.FireLowFlightBlocked+",\""+flightMotor.LastCommand.Move.ToString()+"\","+flightMotor.IsImpactStunned+","+flightMotor.IsGrounded+","+flightMotor.Body.linearDamping+","+flightMotor.Body.maxLinearVelocity);
     File.WriteAllLines(folder+"/motion.csv",motion);File.WriteAllLines(folder+"/physics-contacts.txt",contacts);
     bool freeTravel=currentSpeed>8&&!flightMotor.FireLowFlightBlocked;
     if(freeTravel)freeTravelFrames++;
     if(i>10&&currentSpeed>2)
     {
      Assert.That(bow.Visible,Is.True);
      float error=Vector3.Distance(bow.Center,(head.position+hips.position)*.5f);peakError=Mathf.Max(peakError,error);
      Assert.That(error,Is.LessThan(.08f),"Heating sheath must follow current posed bones, not a guessed elevated motor root.");
      Assert.That(bow.SupportBounds.Contains(head.position),Is.True);Assert.That(bow.SupportBounds.Contains(hips.position),Is.True);
      Assert.That(Vector3.Distance(bow.HeadPoint,head.position),Is.LessThan(.08f));
      peakMs=System.Math.Max(peakMs,bow.LastStepMilliseconds);
     }
     if(freeTravel&&(freeTravelFrames==6||freeTravelFrames==12||freeTravelFrames==18))
     {
      captures++;var image=ScreenCapture.CaptureScreenshotAsTexture();
      try{File.WriteAllBytes(folder+"/view-"+cameraAngle+"-frame-"+i+".png",image.EncodeToPNG());}finally{Object.Destroy(image);}
     }
    }
    File.WriteAllText(folder+"/pre-assert-metrics.txt",$"peakSpeed={peakSpeed}\nfreeTravelFrames={freeTravelFrames}\ncontactFrames={contactFrames}\ntravel={Vector3.Distance(began,flightMotor.Body.position)}\nbodyAnchorError={peakError}\n");
    Assert.That(peakSpeed,Is.GreaterThan(8));
    Assert.That(freeTravelFrames,Is.EqualTo(18),"Need 18 actual fast unblocked frames, not just an enabled VFX flag.");
    Assert.That(captures,Is.EqualTo(3));Assert.That(Vector3.Distance(began,flightMotor.Body.position),Is.GreaterThan(2),"Actual body must travel at least 2m while attachment is checked.");
    if(showcase)
    {
     // Release the real gesture and record natural braking/landing and cooling.
     // No synthetic motion, invulnerability, collider bypass or looping particles.
     flightAbility.SetLowFlightHeld(false);input.Move=float2.zero;
     for(int cool=0;cool<72;cool++)
     {
      Vector3 body=(head.position+hips.position)*.5f;
      capture.Place(body-direction*5+up*2+Vector3.Cross(up,direction).normalized*.65f,body+direction*.15f);
      yield return new WaitForEndOfFrame();
      if(showcaseTick++%5==0)SaveMeteorShowcaseFrame(showcaseFolder,showcaseFrame++);
     }
     File.WriteAllText(showcaseFolder+"/capture.txt","12 fps actual saved scene; forward controller gesture, real support/obstacles, then release/cooling.\nframes="+showcaseFrame+"\nproof="+folder+"\n");
    }
    File.WriteAllText(folder+"/measurements.txt",$"peakSpeed={peakSpeed}\npeakBodyAnchorError={peakError}\npeakUploadMs={peakMs}\nraySteps={FireMeteorBowRenderer.RaySteps}\nfreeTravelFrames={freeTravelFrames}\ncontactFrames={contactFrames}\ntravelMetres={Vector3.Distance(began,flightMotor.Body.position)}\n");
   }
   finally
   {
    if(Directory.Exists(folder)){File.WriteAllLines(folder+"/motion.csv",motion);File.WriteAllLines(folder+"/physics-contacts.txt",contacts);}
    if(worldImpact!=null&&heatObserver!=null)worldImpact.ContactAccepted-=heatObserver;
    foreach(var probe in probes)if(probe!=null)Object.Destroy(probe);
    if(relocated&&flightMotor!=null){flightAbility.CancelAll();flightMotor.Body.position=originalPosition;flightMotor.Body.rotation=originalRotation;flightMotor.Body.linearVelocity=originalVelocity;flightMotor.Body.angularVelocity=originalAngular;flightMotor.ResetAfterTeleport();Physics.SyncTransforms();}
    Time.captureDeltaTime=saved;if(capture!=null)Object.Destroy(capture);
    if(flightMotor!=null){cameraFrameField.SetValue(flightMotor,oldFrame);if(original!=null)flightMotor.ConfigureInputSource(original);}
    if(frame!=null)Object.Destroy(frame);
    if(input!=null)Object.Destroy(input);rival?.Dispose();ReleaseFireAbilityFixture();
   }
  }
  private static void SaveMeteorShowcaseFrame(string folder,int frame)
  {
   var image=ScreenCapture.CaptureScreenshotAsTexture();
   try{File.WriteAllBytes(folder+"/frame"+frame.ToString("D4")+".jpg",image.EncodeToJPG(92));}
   finally{Object.Destroy(image);}
  }

 }
 public sealed class FireLowFlightContactRecorder:MonoBehaviour
 {
  public System.Collections.Generic.List<string> Records;
  private void OnCollisionEnter(Collision collision)=>Record(collision,"enter");
  private void OnCollisionStay(Collision collision)=>Record(collision,"stay");
  private void Record(Collision collision,string kind)
  {
   if(Records==null||Records.Count>=400)return;
   Vector3 normal=collision.contactCount>0?collision.GetContact(0).normal:Vector3.zero;
   Records.Add(Time.fixedTime.ToString("F5")+" "+kind+" body="+name+" other="+collision.collider.name+" impulse="+collision.impulse.ToString("F4")+" relative="+collision.relativeVelocity.ToString("F4")+" normal="+normal.ToString("F4"));
  }
 }
}
