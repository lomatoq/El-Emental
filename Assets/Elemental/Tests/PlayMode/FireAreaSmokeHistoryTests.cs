using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed class FireAreaSmokeHistoryTests
 {
  [UnityTest]public IEnumerator EmittedSurfaceSmokeSpansPatchStaysInWorldAndExpiresAfterFrameHitch()
  {
   GameObject host=new GameObject("Smoke history fixture"),source=new GameObject("Moving burning surface"),view=new GameObject("Smoke history camera");
   object renderer=null;
   var type=typeof(Elemental.Presentation.Fire.FireSmolderPresentation).Assembly.GetType("Elemental.Presentation.Fire.FireSurfaceFlameRenderer");
   try
   {
    var camera=view.AddComponent<Camera>();camera.enabled=false;view.transform.position=new Vector3(0,2,-5);
    var material=Resources.Load<Material>("FireFlipbookAccentMaterial");Assert.That(material,Is.Not.Null);
    renderer=System.Activator.CreateInstance(type,new object[]{host.transform,material,0});
    var points=new Vector3[16];var normals=new Vector3[16];for(int i=0;i<16;i++){points[i]=new Vector3((i%4)*.4f,0,(i/4)*.4f);normals[i]=Vector3.up;}
    var step=type.GetMethod("Step");
    void Tick(float time,float heat)=>step.Invoke(renderer,new object[]{source.transform,points,normals,16,Vector3.up,heat,time,camera,null});
    for(int i=0;i<=126;i++)Tick(i/60f,1);
    var puffs=(System.Array)type.GetField("smokePuffs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(renderer);
    var positions=new Vector3[puffs.Length];Vector3 min=Vector3.one*100,max=Vector3.one*-100;
    for(int i=0;i<puffs.Length;i++){object puff=puffs.GetValue(i);Assert.That((bool)puff.GetType().GetField("Active").GetValue(puff),Is.True);positions[i]=(Vector3)puff.GetType().GetField("Position").GetValue(puff);min=Vector3.Min(min,positions[i]);max=Vector3.Max(max,positions[i]);}
    Assert.That(Vector3.ProjectOnPlane(max-min,Vector3.up).magnitude,Is.GreaterThan(.9f),"Smoke must cover the heated area.");
    source.transform.position+=Vector3.right*10;Tick(2.1f,0);
    for(int i=0;i<puffs.Length;i++){object puff=puffs.GetValue(i);var world=(Vector3)puff.GetType().GetField("Position").GetValue(puff);Assert.That(Vector3.Distance(world,positions[i]),Is.LessThan(.001f),"Already emitted smoke followed its moved receiver.");}
    Tick(5.1f,0);Assert.That((int)type.GetProperty("VisibleCards").GetValue(renderer),Is.Zero,"Smoke must expire by elapsed time even after a slow frame.");
    Tick(5.2f,1);type.GetMethod("Clear").Invoke(renderer,null);
    Assert.That((int)type.GetProperty("VisibleCards").GetValue(renderer),Is.Zero);
   }
   finally{if(renderer!=null)type.GetMethod("Dispose").Invoke(renderer,null);Object.Destroy(host);Object.Destroy(source);Object.Destroy(view);}
   yield return null;
  }
 }
}
