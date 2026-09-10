using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed class EarthHeldMaterialShedRuntimeTests
 {
  [UnityTest] public IEnumerator ActualGravityCaptureContinuouslyShedsNewDustAndFallingChipsThenStops()
  {
#if UNITY_EDITOR
   var root=new GameObject("Held material shed fixture");root.SetActive(false);
   var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);
   EarthEffectsTuningProfile profile=null;
   try
   {
    profile=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset"));
    Assert.That(profile,Is.Not.Null);
    var dustObject=new GameObject("Shed new dust");dustObject.transform.SetParent(root.transform);var dust=dustObject.AddComponent<ParticleSystem>();
    var chipObject=new GameObject("Shed chips");chipObject.transform.SetParent(root.transform);var chips=chipObject.AddComponent<ParticleSystem>();
    var hub=root.AddComponent<EarthMaterialFeedbackHub>();hub.Configure(profile,null);
    var presenter=root.AddComponent<EarthMaterialFeedbackPresenter>();presenter.Configure(hub,profile,null,dust,chips,null);
    var executor=root.AddComponent<MagicExecutor>();executor.ConfigureMaterialFeedback(hub);
    root.transform.position=new Vector3(0,100,0);
    var body=stone.AddComponent<Rigidbody>();body.useGravity=false;
    var fragment=stone.AddComponent<EarthFragment>();fragment.Initialize(0xDA570001u,null,new Vector3(0,100,2),.45f,60f);
    root.SetActive(true);Physics.SyncTransforms();
    int shedCues=0;hub.Presented+=cue=>{if(cue.Kind==EarthMaterialFeedbackKind.AirborneShed)shedCues++;};
    Assert.That(executor.TryBeginGravityWell(stone.GetComponent<Collider>(),new Vector3(0,101,2),Vector3.up,true),Is.True);
    var fixedStep=new WaitForFixedUpdate();
    for(int frame=0;frame<45;frame++)yield return fixedStep;
    yield return null;
    Assert.That(shedCues,Is.GreaterThanOrEqualTo(3),"Capture requires ongoing material cues, not only one Assemble burst.");
    Assert.That(dust.particleCount,Is.GreaterThan(0),"Authored new dust must run during holding.");
    Assert.That(chips.particleCount,Is.GreaterThan(0),"Real chip pool must receive held-stone shed events.");
    var particles=new ParticleSystem.Particle[128];int count=chips.GetParticles(particles);bool falling=false;
    for(int i=0;i<count;i++)if(Vector3.Dot(particles[i].velocity,particles[i].position.normalized)<-.2f)falling=true;
    Assert.That(falling,Is.True,"Shed chips must fall under local gravity rather than only orbit or emit upward.");
    executor.CancelGravityWell();yield return null;int stopped=shedCues;
    for(int frame=0;frame<20;frame++)yield return fixedStep;
    yield return null;Assert.That(shedCues,Is.EqualTo(stopped),"Released capture cannot keep shedding.");
   }
   finally{Object.Destroy(stone);Object.Destroy(root);if(profile!=null)Object.Destroy(profile);}
#else
   Assert.Ignore("Uses authored effects profile.");yield break;
#endif
  }
 }
}
