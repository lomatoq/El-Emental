using System.Collections;
using System.Linq;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(240000)] public IEnumerator ActualColumnFlameRemainsDetailedAcrossLateCameraOrbit()
  {
   FireVisualCaptureCamera capture=null;float saved=Time.captureDeltaTime;FireFlowVolumeBackend[] flows=null;System.IDisposable safeRival=null;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safeRival=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var columns=All<ArenaColumnFires>().Single();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    flows=(FireFlowVolumeBackend[])typeof(ArenaColumnFires).GetField("fire",flags).GetValue(columns);
    var seats=(ArenaColumnFires.Seat[])typeof(ArenaColumnFires).GetField("seats",flags).GetValue(columns);
    int index=System.Array.FindIndex(seats,x=>x.Source!=null&&x.Source.enabled);Assert.That(index,Is.GreaterThanOrEqualTo(0));
    Vector3 up=seats[index].Up.normalized,point=seats[index].Source.transform.TransformPoint(seats[index].LocalPoint)+up*.65f;
    Vector3 side=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.right).normalized,across=Vector3.Cross(up,side);
    var view=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();
    for(int angle=0;angle<3;angle++)
    {
     float phase=angle*Mathf.PI*.5f;capture.Place(point+(side*Mathf.Cos(phase)+across*Mathf.Sin(phase))*3+up*.4f,point);
     yield return new WaitForSeconds(.15f);Time.captureDeltaTime=1f/1000000;
     yield return new WaitForEndOfFrame();SaveFireAbilityFrame("column-camera-orbit-"+angle);
     var solver=flows[index].Solver;float hotHeight=0;int hot=0,cooling=0;
     Vector3 cap=seats[index].Source.transform.TransformPoint(seats[index].LocalPoint);
     for(int particle=0;particle<solver.Count;particle++){var gas=solver.Particles[particle];if(gas.Spark)continue;if(gas.Age<gas.HotLifetime){hot++;hotHeight=Mathf.Max(hotHeight,Vector3.Dot((Vector3)gas.Position-cap,up));}else cooling++;}
     string diagnostic="BuildReports/HardPolish/G05/FireAbilities/column-transport-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+angle+".csv";
     System.IO.File.WriteAllText(diagnostic,"hot,cooling,hotHeight,blocked,rejected,queries,budgetStops,smokeSpan\n"+hot+","+cooling+","+hotHeight+","+solver.BlockedParcels+","+solver.RejectedBirths+","+solver.QueryCount+","+solver.BudgetStops+","+flows[index].AccentSmokeSpan);
     Assert.That(hotHeight,Is.GreaterThan(.8f),"Column must develop a rising hot plume beyond its tiny birth sphere.");
     Assert.That(cooling,Is.GreaterThan(0),"A continuous column must retain transported cooling smoke.");
     Assert.That(flows[index].AccentCards,Is.GreaterThan(0));var on=ScreenCapture.CaptureScreenshotAsTexture();
     flows[index].SetAccentRenderingForQa(false);yield return new WaitForEndOfFrame();var off=ScreenCapture.CaptureScreenshotAsTexture();
     try{var a=on.GetPixels32();var b=off.GetPixels32();int changed=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>5)changed++;Assert.That(changed,Is.GreaterThan(40),"Column accents must render from every late camera view.");}
     finally{Object.Destroy(on);Object.Destroy(off);flows[index].SetAccentRenderingForQa(true);Time.captureDeltaTime=1f/60;}
    }
   }
   finally{if(flows!=null)foreach(var flow in flows)flow.SetAccentRenderingForQa(true);safeRival?.Dispose();if(capture!=null)Object.Destroy(capture);Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
  [UnityTest,Timeout(240000)] public IEnumerator FootAndColumnsRenderActualAtlasDetailAndClear()
  {
   float scale=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60f;yield return ReadyFireAbilities();flightAbility.SetLiftHeld(true);yield return new WaitForSeconds(.65f);
    var effects=binding.GetComponent<FireAbilityEffects>();
    var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    var flows=(FireFlowVolumeBackend[])typeof(FireAbilityEffects).GetField("flows",flags).GetValue(effects);
    Assert.That(flows[0].AccentCards+flows[1].AccentCards,Is.GreaterThan(0));
    var footMaterial=(Material)typeof(FireFlowVolumeBackend).GetField("material",flags).GetValue(flows[0]);
    Assert.That(footMaterial.GetFloat("_DetailMix"),Is.Zero,"Feet retain the hand stream material path.");
    Assert.That(footMaterial.GetColor("_Core").g,Is.GreaterThan(4),"Fresh foot nozzle retains its requested near-white core.");
    var columns=All<ArenaColumnFires>().Single();
    var columnFlows=(FireFlowVolumeBackend[])typeof(ArenaColumnFires).GetField("fire",flags).GetValue(columns);
    Assert.That(columnFlows.Count(x=>x.AccentCards>0),Is.GreaterThan(0));
    var renderers=effects.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name=="Transported flame and smoke atlas accents"&&x.enabled).ToArray();
    Assert.That(renderers.Length,Is.GreaterThan(0));Time.captureDeltaTime=1f/100000f;
    yield return new WaitForEndOfFrame();SaveFireAbilityFrame("foot-atlas-detail");var on=ScreenCapture.CaptureScreenshotAsTexture();
    foreach(var flow in flows)flow.SetAccentRenderingForQa(false);
    yield return new WaitForEndOfFrame();var off=ScreenCapture.CaptureScreenshotAsTexture();
    try{var a=on.GetPixels32();var b=off.GetPixels32();int changed=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>5)changed++;
     Assert.That(changed,Is.GreaterThan(40),"Atlas accents must affect actual foot-fire pixels.");}
    finally{Object.Destroy(on);Object.Destroy(off);foreach(var flow in flows)flow.SetAccentRenderingForQa(true);Time.captureDeltaTime=scale;}
    flightAbility.CancelAll();yield return new WaitForSeconds(1);
    Assert.That(flows.Sum(x=>x.AccentCards),Is.Zero);
   }
   finally{Time.captureDeltaTime=scale;ReleaseFireAbilityFixture();}
  }
  [UnityTest,Timeout(240000)] public IEnumerator ActualFireballImpactFlashesThenLeavesCoolingSmoke()
  {
   GameObject target=null;float step=Time.captureDeltaTime;System.Action<Elemental.Simulation.Fire.FireAbilityCue> handler=null;
   try
   {
    Time.captureDeltaTime=1f/60f;yield return ReadyFireAbilities();
    Vector3 origin=binding.PlayerSession.MuzzlePosition,up=flightMotor.LocalUp;
    target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Actual fireball impact receiver";target.transform.position=origin+up*3;Physics.SyncTransforms();
    float hit=-1;handler=cue=>{if(cue.Kind==Elemental.Simulation.Fire.FireAbilityEffectKind.Impact)hit=Time.time;};flightAbility.Effect+=handler;
    Assert.That(flightAbility.TryShoot(target.transform.position,false),Is.True);
    float deadline=Time.time+3;while(hit<0&&Time.time<deadline)yield return null;
    Assert.That(hit,Is.GreaterThanOrEqualTo(0));yield return new WaitForSeconds(.03f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("actual-impact-flash");
    yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();
    Assert.That(binding.GetComponent<FireAbilityLighting>().ActiveLights,Is.Zero,"Impact light must not linger as an ordinary burning lamp.");
    SaveFireAbilityFrame("actual-impact-cooling");
   }
   finally{if(handler!=null&&flightAbility!=null)flightAbility.Effect-=handler;if(target!=null)Object.Destroy(target);Time.captureDeltaTime=step;ReleaseFireAbilityFixture();}
  }
 }
}
