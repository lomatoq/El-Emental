using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode {
 public sealed partial class HardPolishFireStreamBindingRuntimeTests {
  [UnityTest,Timeout(180000)] public IEnumerator SavedTowerFireUsesBoundedTransportAndPreservesSourcePauseAndLights() {
   // Towers are environmental visuals, independent of hand-channel admission.
   Assert.That(flow.BeginBot(),Is.True);double deadline=Time.realtimeSinceStartupAsDouble+30;
   while((flow.State!=Elemental.Presentation.UI.FrontendState.Combat||!duel.CombatAllowed)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
   Assert.That(flow.State,Is.EqualTo(Elemental.Presentation.UI.FrontendState.Combat));Assert.That(duel.CombatAllowed,Is.True);
   foreach(var bot in All<Elemental.Runtime.Characters.EarthMvpBotController>())bot.enabled=false;
   yield return new WaitForSecondsRealtime(1.5f);
   var owner=All<ArenaColumnFires>().Single();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var seats=(ArenaColumnFires.Seat[])typeof(ArenaColumnFires).GetField("seats",flags).GetValue(owner);
   var gases=(FireFlowVolumeBackend[])typeof(ArenaColumnFires).GetField("fire",flags).GetValue(owner);
   var lamps=(Light[])typeof(ArenaColumnFires).GetField("lights",flags).GetValue(owner);
   Assert.That(gases.Length,Is.EqualTo(seats.Length));Assert.That(lamps.Count(x=>x.enabled),Is.LessThanOrEqualTo(4));
   int selected=-1;
   for(int i=0;i<gases.Length;i++) {Assert.That(gases[i].Solver.ParticleLimit,Is.EqualTo(32));if(!seats[i].Source.enabled||!seats[i].Source.gameObject.activeInHierarchy)continue;
    Assert.That(gases[i].Solver.Count,Is.GreaterThan(0),"Saved cap produced no transported gas: "+i);Assert.That(gases[i].Visible,Is.True);selected=i;}
   Assert.That(selected,Is.GreaterThanOrEqualTo(0));
   var gas=gases[selected];float scale=Time.timeScale;bool sourceEnabled=seats[selected].Source.enabled;
   var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;FireVisualCaptureCamera pose=null;
   var resolution=new ProductionCaptureResolution();
   try {
    Time.timeScale=0;int count=gas.Solver.Count;var first=gas.Solver.Particles[0].Position;
    for(int n=0;n<8;n++)yield return new WaitForEndOfFrame();
    Assert.That(gas.Solver.Count,Is.EqualTo(count));Assert.That(Unity.Mathematics.math.distance(first,gas.Solver.Particles[0].Position),Is.Zero);
    yield return resolution.WaitForRenderedSize(camera);pose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
    Vector3 point=seats[selected].Source.transform.TransformPoint(seats[selected].LocalPoint),up=seats[selected].Up.normalized;
    Vector3 side=Vector3.Cross(up,camera.transform.forward).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
    string folder="BuildReports/HardPolish/G05/TowerTransport-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
    for(int view=0;view<3;view++){pose.Place(point+Quaternion.AngleAxis(view*70,up)*side*5+up,point+up*.8f);for(int n=0;n<3;n++)yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/tower-"+view+".png",image.EncodeToPNG());Object.Destroy(image);}
    seats[selected].Source.enabled=false;yield return null;Assert.That(gas.Solver.Count,Is.Zero);Assert.That(gas.Visible,Is.False);Assert.That(lamps[selected].enabled,Is.False);
    seats[selected].Source.enabled=true;yield return null;Assert.That(gas.Solver.Count,Is.GreaterThan(0));
   }finally {seats[selected].Source.enabled=sourceEnabled;Time.timeScale=scale;if(pose!=null)Object.Destroy(pose);camera.transform.SetPositionAndRotation(pos,rot);resolution.Dispose();}
  }
 }
}
