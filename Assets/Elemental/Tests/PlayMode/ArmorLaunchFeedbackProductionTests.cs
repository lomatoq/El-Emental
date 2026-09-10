using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Elemental.Input.Gestures;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode {
 public sealed partial class HardPolishFireStreamBindingRuntimeTests {
  [UnityTest,Timeout(240000)] public IEnumerator ActualArmorShotsAlwaysReleaseVisibleMatterAndEmitDepartureChips(){
   DirectFireInputLease lease=null;EarthMaterialFeedbackHub hub=null;Action<EarthMaterialFeedbackCue> cue=null;
   try{yield return EnterCombat();var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);lease=new DirectFireInputLease(input);
    var armor=duel.PlayerTransform.GetComponent<EarthArmorController>();Assert.That(armor.Begin(),Is.True);
    // Formation phase is selected by the real scroll grammar; time only assembles plates.
    for(int notch=0;notch<4;notch++) Assert.That(armor.ApplyWheel(120f,Time.unscaledTime),Is.EqualTo(EarthArmorInputResult.PhaseChanged));
    Assert.That(armor.Phase01,Is.GreaterThan(.35f));
    float assemblyStart=Time.fixedTime;double deadline=Time.realtimeSinceStartupAsDouble+10;
    while((Time.fixedTime-assemblyStart<.4f||armor.ControllablePieceCount<3)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
    Assert.That(Time.fixedTime-assemblyStart,Is.GreaterThanOrEqualTo(.4f),"Armor preparation physics did not advance.");
    Assert.That(armor.ControllablePieceCount,Is.GreaterThanOrEqualTo(3),"Actual armor must prepare three real plates before firing.");
    hub=All<EarthMaterialFeedbackHub>().Single();var presenter=All<EarthMaterialFeedbackPresenter>().Single();var chips=(ParticleSystem)typeof(EarthMaterialFeedbackPresenter).GetField("chips",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(presenter);
    int launches=0;cue=x=>{if(x.Kind==EarthMaterialFeedbackKind.Release&&x.ChipCount>0)launches++;};hub.Presented+=cue;
    var pieces=new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
    for(int shot=0;shot<3;shot++){int count=armor.CopyActivePiecesNonAlloc(pieces);foreach(var piece in pieces.Take(count))if(!piece.IsReleased)piece.SetCameraSuppressed(true);
     var before=pieces.Take(count).Where(x=>x.IsReleased).ToHashSet();
     int previous=launches;Assert.That(armor.FireNearestAtPoint(duel.PlayerTransform.position+duel.PlayerTransform.up*20),Is.True);
     var released=pieces.Take(count).Single(x=>x.IsReleased&&!before.Contains(x));Assert.That(released.CameraSuppressed,Is.False,"Launch must release a previously camera-hidden plate immediately.");
     yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();Assert.That(launches,Is.GreaterThan(previous));Assert.That(chips.particleCount,Is.GreaterThan(0),"Canonical departure cue reached hub but no chip particles rendered.");
    }
   }finally{if(hub!=null&&cue!=null)hub.Presented-=cue;lease?.Dispose();}
  }
 }
}
