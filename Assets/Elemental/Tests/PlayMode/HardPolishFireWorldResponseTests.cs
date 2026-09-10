using System.Collections;
using System.Reflection;
using Elemental.Input.Gestures;
using Elemental.Runtime.Fire;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using Elemental.Presentation.VFX;
using System.Linq;
using System.IO;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using UnityEngine.Rendering.Universal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  private static float ScorchPatchLuminance(UnityEngine.Camera camera,Vector3 point)
  {
   Vector3 pixel=camera.WorldToScreenPoint(point);var texture=new Texture2D(16,16,TextureFormat.RGB24,false);
   try
   {
    texture.ReadPixels(new Rect(Mathf.Clamp((int)pixel.x-8,0,Screen.width-16),Mathf.Clamp((int)pixel.y-8,0,Screen.height-16),16,16),0,0);texture.Apply();
    float sum=0;foreach(var c in texture.GetPixels())sum+=c.r*.2126f+c.g*.7152f+c.b*.0722f;return sum/256;
   }
   finally{Object.Destroy(texture);}
  }
  [UnityTest,Timeout(300000)] public IEnumerator SavedFireScorchTracksMovingSurfaceAndExpires()
  {
   yield return EnterCombat();
   var executor=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true).EarthExecutor;
   var scar=All<EarthSurfaceScarPool>().First(p=>p.ConfiguredExecutor==executor);
   var go=new GameObject("Scorch authority fixture");var response=go.AddComponent<FireWorldImpact>();
   response.Configure(duel,EarthDuelFighterId.Player,executor.FireDebrisPool,executor.FireFeedbackHub);
   // The production pool may already have its two owners bound; use a dedicated
   // copy of its configured material/profile with the same existing pool implementation.
   var scratchObject=new GameObject("Scorch pool fixture");var scratch=scratchObject.AddComponent<EarthSurfaceScarPool>();
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   scratch.Configure(executor,(EarthFeedbackProfile)typeof(EarthSurfaceScarPool).GetField("profile",flags).GetValue(scar),
       (Material)typeof(EarthSurfaceScarPool).GetField("decalMaterial",flags).GetValue(scar),null);
   scratch.ConfigureFire(response);
   var secondary=new GameObject("Second configured heat source").AddComponent<FireWorldImpact>();secondary.transform.SetParent(go.transform,false);
   secondary.Configure(duel,EarthDuelFighterId.Bot,executor.FireDebrisPool,executor.FireFeedbackHub);
   var charMask=go.AddComponent<Elemental.Presentation.Fire.FireSmolderPresentation>();
   charMask.Configure(response,secondary,UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/WallcoeurEarthDust.mat"));
   var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);
#if UNITY_EDITOR
   var productionStone=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
   Assert.That(productionStone,Is.Not.Null);surface.GetComponent<MeshRenderer>().sharedMaterial=productionStone;
#endif
   surface.transform.position=duel.PlayerTransform.position+duel.PlayerTransform.up*12;
   var camera=All<CelestialSystemBehaviour>().Single().TargetCamera;
   using var captureSize=new ProductionCaptureResolution();yield return captureSize.WaitForRenderedSize(camera);
   var capturePose=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
   Vector3 oldCameraPosition=camera.transform.position;Quaternion oldCameraRotation=camera.transform.rotation;
   string folder="BuildReports/HardPolish/FireWorldScorch-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
   try
   {
    capturePose.Place(surface.transform.position+Vector3.forward*3,surface.transform.position);
    Physics.SyncTransforms();var shape=surface.GetComponent<Collider>();Vector3 point=shape.bounds.center+Vector3.forward*.5f;
    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
    ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"scorch-unmarked-production-stone.png"));
    float unmarked=ScorchPatchLuminance(camera,point);
    Assert.That(response.ApplyContact(shape,point,Vector3.forward,Vector3.back,.02f),Is.True);
    yield return null;
    var projector=scratch.GetComponentsInChildren<DecalProjector>().Single(p=>p.enabled);
    Assert.That(projector.pivot,Is.EqualTo(Vector3.zero),"Thin volume must straddle its contact instead of retaining the default .5m offset.");
    Assert.That(projector.material.GetTexture("Base_Map"),Is.Not.Null);
    yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"scorch-live.png"));
    float marked=ScorchPatchLuminance(camera,point);
    File.WriteAllText(Path.Combine(folder,"receiver.txt"),"RumbleSandstone unmarked="+unmarked+" marked="+marked);
    Assert.That(marked,Is.LessThan(unmarked*.85f),"Production rock shader must visibly receive soot; projector enabled alone is insufficient.");
    Vector3 before=projector.transform.position;Quaternion facing=projector.transform.rotation;
    var burnBlock=new MaterialPropertyBlock();surface.GetComponent<Renderer>().GetPropertyBlock(burnBlock);
    Vector4 originalBurnPoint=burnBlock.GetVector("_FireBurnPoint");
    for(int hitIndex=0;hitIndex<12;hitIndex++)
    {
     Assert.That(response.ApplyContact(shape,point+Vector3.right*.25f,Vector3.forward,Vector3.back,.02f),Is.True);
     yield return null;
    }
    Assert.That(Vector3.Distance(projector.transform.position,before),Is.LessThan(.0001f),"Repeated nearby fire hits must not drag an existing soot footprint.");
    Assert.That(Quaternion.Angle(projector.transform.rotation,facing),Is.LessThan(.001f));
    surface.GetComponent<Renderer>().GetPropertyBlock(burnBlock);
    Assert.That(burnBlock.GetVector("_FireBurnPoint"),Is.EqualTo(originalBurnPoint),"The receiver's material char mask must stay fixed too, not chase the latest heat contact.");
    surface.transform.position+=Vector3.right;
    yield return null;yield return null;
    Assert.That(Vector3.Distance(projector.transform.position,before+Vector3.right),Is.LessThan(.001f));
    capturePose.Place(surface.transform.position+Vector3.forward*3,surface.transform.position);
    yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"scorch-moved.png"));
    yield return new WaitForSeconds(5f);yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"scorch-fading.png"));
    yield return new WaitForSeconds(2.2f);yield return null;
    Assert.That(projector.enabled,Is.False,"Fire marks must fade and retire even when Earth scars are persistent.");
    yield return new WaitForEndOfFrame();ProductionCaptureResolution.SaveScreen(Path.Combine(folder,"scorch-expired.png"));
   }
   finally{Object.Destroy(capturePose);camera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);Object.Destroy(surface);Object.Destroy(go);Object.Destroy(scratchObject);}
  }
  [UnityTest,Timeout(300000)] public IEnumerator SavedActualFireStreamBurnsCanonicalSmallRockThroughNearestCoverHook()
  {
   yield return EnterCombat();var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);
   Assert.That(input.TrySelectElement(ElementId.Fire),Is.True);
   using var lease=new DirectFireInputLease(input);
   var executor=input.EarthExecutor;var pool=(EarthFragmentPool)typeof(MagicExecutor).GetField("fragmentPool",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(executor);
   var session=binding.PlayerSession;var field=typeof(FireStreamSession).GetField("worldImpact",BindingFlags.Instance|BindingFlags.NonPublic);
   var oldImpact=(FireWorldImpact)field.GetValue(session);var go=new GameObject("Stream rock response");var response=go.AddComponent<FireWorldImpact>();
   response.Configure(duel,EarthDuelFighterId.Player,executor.FireDebrisPool,executor.FireFeedbackHub);session.ConfigureWorldImpact(response);
   Vector3 direction=FindClearFireDirection(duel.PlayerTransform,session.MuzzlePosition);
   var stone=pool.Acquire(executor,session.MuzzlePosition+direction*2.5f,.22f,10);Assert.That(stone,Is.Not.Null);
   stone.StopBendControl();Assert.That(stone.MatterIdentity.TryRead(out var released),Is.True);
   Assert.That(released.Phase,Is.EqualTo(EarthMatterPhase.FreeDynamic),"Acquire begins Forming; release through the actual fragment API before testing fire.");
   var gravity=stone.GetComponent<Elemental.Runtime.Physics.GravityBody>();bool oldGravity=gravity!=null&&gravity.enabled;if(gravity!=null)gravity.enabled=false;
   bool cover=false;
   try
   {
    Physics.SyncTransforms();Assert.That(session.TryBegin(stone.Body.worldCenterOfMass),Is.True);
    for(int frame=0;frame<180&&stone.gameObject.activeSelf;frame++)
    {session.SetAim(stone.Body.worldCenterOfMass);yield return new WaitForFixedUpdate();cover|=session.HasCoverContact;}
    Assert.That(cover,Is.True,"The real session must see its stone as nearest cover.");
    Assert.That(response.Fractures,Is.EqualTo(1),"Fire rejected partitions="+response.RejectedPartitions+" (shared pool latest, possibly unrelated: "+executor.FireDebrisPool.LastBreakRejection+")");
    Assert.That(stone.gameObject.activeSelf,Is.False);
   }
   finally{session.Stop();session.ConfigureWorldImpact(oldImpact);if(gravity!=null)gravity.enabled=oldGravity;if(stone.gameObject.activeSelf)stone.CompleteReintegration();Object.Destroy(go);}
  }
  [UnityTest,Timeout(300000)] public IEnumerator SavedActualFireStreamPushesAndSplitsLargeRockStartingAtNozzle()
  {
   yield return EnterCombat();var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);
   Assert.That(input.TrySelectElement(ElementId.Fire),Is.True);
   using var lease=new DirectFireInputLease(input);
   var executor=input.EarthExecutor;var pool=(EarthFragmentPool)typeof(MagicExecutor).GetField("fragmentPool",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(executor);
   var session=binding.PlayerSession;var field=typeof(FireStreamSession).GetField("worldImpact",BindingFlags.Instance|BindingFlags.NonPublic);
   var oldImpact=(FireWorldImpact)field.GetValue(session);var go=new GameObject("Stream rock response");var response=go.AddComponent<FireWorldImpact>();
   response.Configure(duel,EarthDuelFighterId.Player,executor.FireDebrisPool,executor.FireFeedbackHub);session.ConfigureWorldImpact(response);
   Vector3 direction=FindClearFireDirection(duel.PlayerTransform,session.MuzzlePosition);
   var stone=pool.Acquire(executor,session.MuzzlePosition+direction*.65f,1.3f,600);Assert.That(stone,Is.Not.Null);
   stone.StopBendControl();Assert.That(stone.MatterIdentity.TryRead(out var released),Is.True);
   Assert.That(released.Phase,Is.EqualTo(EarthMatterPhase.FreeDynamic),"Acquire begins Forming; release through the actual fragment API before testing fire.");
   var gravity=stone.GetComponent<Elemental.Runtime.Physics.GravityBody>();bool oldGravity=gravity!=null&&gravity.enabled;if(gravity!=null)gravity.enabled=false;
   bool cover=false;float peakSpeed=0;
   // Isolate nozzle contact from the separate crushing mechanic: a1.3m stone
   // intentionally enclosing the muzzle otherwise kills its caster before heat accrues.
   var stoneShape=stone.GetComponent<Collider>();var ownerShapes=duel.PlayerTransform.GetComponentsInChildren<Collider>(true);
   foreach(var ownerShape in ownerShapes)Physics.IgnoreCollision(stoneShape,ownerShape,true);
   try
   {
    Physics.SyncTransforms();Assert.That(session.TryBegin(stone.Body.worldCenterOfMass),Is.True);
    for(int frame=0;frame<180&&stone.gameObject.activeSelf;frame++)
    {session.SetAim(stone.Body.worldCenterOfMass);yield return new WaitForFixedUpdate();cover|=session.HasCoverContact;if(stone.gameObject.activeSelf)peakSpeed=Mathf.Max(peakSpeed,stone.Body.linearVelocity.magnitude);}
    Assert.That(peakSpeed,Is.GreaterThan(1f),"Large rock must receive visible acceleration before splitting.");
    Assert.That(cover,Is.True,"The real session must see its stone as nearest cover.");
    Assert.That(response.Fractures,Is.EqualTo(1),"CasterHP="+duel.PlayerHealth+" sessionActive="+session.IsActive+" Fire rejected partitions="+response.RejectedPartitions+" (shared pool latest, possibly unrelated: "+executor.FireDebrisPool.LastBreakRejection+")");
    Assert.That(stone.gameObject.activeSelf,Is.False);
   }
   finally{foreach(var ownerShape in ownerShapes)if(stoneShape!=null&&ownerShape!=null)Physics.IgnoreCollision(stoneShape,ownerShape,false);session.Stop();session.ConfigureWorldImpact(oldImpact);if(gravity!=null)gravity.enabled=oldGravity;if(stone.gameObject.activeSelf)stone.CompleteReintegration();Object.Destroy(go);}
  }
  [UnityTest,Timeout(300000)] public IEnumerator FireHeatReleasesAttachedArenaThroughStructuralOwner()
  {
   yield return EnterCombat();var input=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true);
   using var lease=new DirectFireInputLease(input);
   var response=binding.PlayerSession.GetComponent<FireWorldImpact>();Assert.That(response,Is.Not.Null);
   var structure=All<EarthArenaStructure>().First(x=>x.OrdinaryDamageEnabled&&!x.IsFractured);
   var shape=(Collider)typeof(EarthArenaStructure).GetField("intactCollider",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(structure);
   Assert.That(shape,Is.Not.Null);Assert.That(shape.enabled,Is.True);int before=structure.ReleasedPieceCount;
   Vector3 direction=(shape.bounds.center-duel.PlayerTransform.position).normalized;
   for(int i=0;i<100&&structure.ReleasedPieceCount==before;i++)
   {
    Physics.SyncTransforms();Vector3 point=shape.ClosestPoint(shape.bounds.center-direction*30);
    Assert.That(response.ApplyContact(shape,point,-direction,direction,Time.fixedDeltaTime,1),Is.True);
    yield return new WaitForFixedUpdate();
   }
   Assert.That(structure.ReleasedPieceCount,Is.GreaterThan(before),"Intact arena must reach the canonical structural damage route after sustained heat.");
  }
  [UnityTest,Timeout(300000)] public IEnumerator SavedFireContactArchivesSmallAndSplitsMediumLargeThroughCanonicalPool()
  {
   yield return EnterCombat();
   var executor=duel.PlayerTransform.GetComponentInChildren<MagicInputController>(true).EarthExecutor;
   var pool=(EarthFragmentPool)typeof(MagicExecutor).GetField("fragmentPool",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(executor);
   var go=new GameObject("Fire world response fixture");var response=go.AddComponent<FireWorldImpact>();
   response.Configure(duel,EarthDuelFighterId.Player,executor.FireDebrisPool,executor.FireFeedbackHub);
   var stones=new EarthFragment[3];int cues=0;response.ContactAccepted+=c=>cues++;
   try
   {
    for(int i=0;i<3;i++)
    {
     float radius=i==0?.22f:i==1?.65f:1.3f;
     Vector3 origin=duel.PlayerTransform.position+duel.PlayerTransform.up*(10+i*6);
     stones[i]=pool.Acquire(executor,origin,radius,i==0?10:i==1?90:600);
     Assert.That(stones[i],Is.Not.Null);
     stones[i].StopBendControl();Assert.That(stones[i].MatterIdentity.TryRead(out var released),Is.True);
     Assert.That(released.Phase,Is.EqualTo(EarthMatterPhase.FreeDynamic));
     var shape=stones[i].GetComponent<Collider>();
     Physics.SyncTransforms();int before=response.Fractures;
     for(int frame=0;frame<120&&stones[i].gameObject.activeSelf;frame++)
     {
      Vector3 direction=duel.PlayerTransform.forward;
      Vector3 point=shape.ClosestPoint(stones[i].Body.worldCenterOfMass-direction*5);
      Assert.That(response.ApplyContact(shape,point,-direction,direction,Time.fixedDeltaTime,2),Is.True);
      yield return new WaitForFixedUpdate();
     }
     Assert.That(response.Fractures,Is.EqualTo(before+1),"Must commit one canonical archive/split before retiring parent. Rejections: "+"Fire rejected partitions="+response.RejectedPartitions+" (shared pool latest, possibly unrelated: "+executor.FireDebrisPool.LastBreakRejection+")");
     Assert.That(stones[i].gameObject.activeSelf,Is.False);
    }
    Assert.That(cues,Is.GreaterThan(3));
    response.enabled=false;
    Assert.That(response.ApplyContact(duel.PlayerTransform.GetComponentInChildren<Collider>(),Vector3.zero,Vector3.up,Vector3.forward,.02f),Is.False);
   }
   finally{Object.Destroy(go);foreach(var stone in stones)if(stone!=null&&stone.gameObject.activeSelf)stone.CompleteReintegration();}
  }
 }
}
