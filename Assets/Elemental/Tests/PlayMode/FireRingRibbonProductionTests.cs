using System.Collections;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(60000)]public IEnumerator SupportedRingRibbonClosesFreeFloorButNeverBridgesWallOrMissingSupport()
  {
   GameObject owner=null,floor=null,wall=null;FireRingRibbonRenderer ribbon=null;
   try
   {
    owner=new GameObject("Isolated ribbon owner");floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=30;floor.transform.position=new Vector3(0,99.75f,0);floor.transform.localScale=new Vector3(14,.5f,14);
    var points=new Vector3[24];var valid=new bool[24];for(int i=0;i<24;i++){float angle=i*Mathf.PI*2/24;points[i]=new Vector3(Mathf.Cos(angle)*4,100.48f,Mathf.Sin(angle)*4);valid[i]=true;}
    ribbon=new FireRingRibbonRenderer(owner.transform,owner.transform,1<<30);Physics.SyncTransforms();
    ribbon.Step(points,valid,24,new Vector3(0,100,0),Vector3.up,1,0);Assert.That(ribbon.VisibleSpans,Is.EqualTo(24));Assert.That(ribbon.QueryCount,Is.EqualTo(24));Assert.That(ribbon.Saturations,Is.Zero);
    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=30;wall.transform.position=new Vector3(4,101,0);wall.transform.localScale=new Vector3(.2f,2,2);Physics.SyncTransforms();
    ribbon.Step(points,valid,24,new Vector3(0,100,0),Vector3.up,1,.1f);Assert.That(ribbon.VisibleSpans,Is.LessThan(24));Assert.That(ribbon.VisibleSpans,Is.GreaterThan(16));
    wall.SetActive(false);Physics.SyncTransforms();ribbon.Step(points,valid,24,new Vector3(0,100,0),Vector3.up,1,.2f);Assert.That(ribbon.VisibleSpans,Is.EqualTo(24));
    valid[7]=false;ribbon.Step(points,valid,24,new Vector3(0,100,0),Vector3.up,1,.3f);Assert.That(ribbon.VisibleSpans,Is.EqualTo(22));
    ribbon.Step(points,valid,24,new Vector3(0,100,0),Vector3.up,0,.4f);Assert.That(ribbon.VisibleSpans,Is.Zero);yield return null;
   }
   finally{ribbon?.Dispose();if(owner!=null)Object.Destroy(owner);if(floor!=null)Object.Destroy(floor);if(wall!=null)Object.Destroy(wall);}
  }
 }
}
