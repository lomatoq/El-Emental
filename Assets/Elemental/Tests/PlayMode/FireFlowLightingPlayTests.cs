using System.Collections;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
 public sealed class FireFlowLightingPlayTests
 {
  [UnityTest] public IEnumerator ExistingPoolBoundsTwoStreamsAndReleasesEverySample()
  {
   var root=new GameObject("Owned six-light flow pool proof");var first=new GameObject("ownerA");var second=new GameObject("ownerB");var extra=new GameObject("ownerC");
   try
   {
    var pool=root.AddComponent<FireLightPool>();pool.ReserveStreamLighting();
    pool.PublishFlow(first,Vector3.zero,Vector3.right*2,Vector3.right*4,3,4,3);
    pool.PublishFlow(second,Vector3.forward*2,new Vector3(2,0,2),new Vector3(4,0,2),3,4,3);
    Assert.That(root.GetComponentsInChildren<Light>(true).Length,Is.EqualTo(6));Assert.That(pool.ActiveLights,Is.EqualTo(6));
    pool.PublishFlow(extra,Vector3.up,Vector3.up*2,Vector3.up*3,3,4,3);Assert.That(pool.ActiveLights,Is.EqualTo(6));
    pool.Release(first);Assert.That(pool.ActiveLights,Is.EqualTo(3));
    pool.PublishFlow(second,Vector3.forward,Vector3.zero,Vector3.zero,1,4,3);Assert.That(pool.ActiveLights,Is.EqualTo(1));
    pool.Release(second);Assert.That(pool.ActiveLights,Is.Zero);
    pool.PublishFlow(first,Vector3.zero,Vector3.right,Vector3.right*2,3,4,3);pool.enabled=false;Assert.That(pool.ActiveLights,Is.Zero);
    var shader=Resources.Load<Shader>("FireFlowParcel");Assert.That(shader,Is.Not.Null);var material=new Material(shader);
    try{Assert.That(material.FindPass("FlowHeatHaze"),Is.GreaterThanOrEqualTo(0));}finally{Object.Destroy(material);}
    yield return null;
   }
   finally{Object.Destroy(root);Object.Destroy(first);Object.Destroy(second);Object.Destroy(extra);}
  }
 }
}
