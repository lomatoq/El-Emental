using System;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Authoring.Editor;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class StartupCachePoolOwnershipTests
    {
        private readonly List<GameObject> owned=new List<GameObject>();
        private T Make<T>(string name)where T:Component
        {var go=new GameObject(name);go.SetActive(false);owned.Add(go);return go.AddComponent<T>();}
        private static EarthRockDebrisPool Resolve(IReadOnlyList<EarthRockDebrisPool> pools,EarthSceneReadinessGate gate)
        {
            return (EarthRockDebrisPool)typeof(StartupCacheBaker).GetMethod("ResolveReadinessPool",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{pools,gate});
        }
        [TestCase(false)] [TestCase(true)]
        public void TwoPoolsHonorTheSavedGateRatherThanEnumerationOrder(bool reverse)
        {
            var local=Make<EarthRockDebrisPool>("Local");var online=Make<EarthRockDebrisPool>("Online Two");
            var gate=Make<EarthSceneReadinessGate>("Readiness");gate.Configure(null,local,Array.Empty<Behaviour>());
            var pools=reverse?new[]{online,local}:new[]{local,online};
            Assert.That(Resolve(pools,gate),Is.SameAs(local));
        }
        [Test] public void AmbiguousMultiplePoolsWithoutGateFailBeforeCachePublication()
        {
            var pools=new[]{Make<EarthRockDebrisPool>("Local"),Make<EarthRockDebrisPool>("Online Two")};
            var error=Assert.Throws<TargetInvocationException>(()=>Resolve(pools,null));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        }
        [Test] public void GatePointingOutsideTheEnumeratedSceneIsRejected()
        {
            var local=Make<EarthRockDebrisPool>("Local");var foreign=Make<EarthRockDebrisPool>("Foreign");
            var gate=Make<EarthSceneReadinessGate>("Readiness");gate.Configure(null,foreign,Array.Empty<Behaviour>());
            var error=Assert.Throws<TargetInvocationException>(()=>Resolve(new[]{local},gate));
            Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
        }
        [TearDown] public void Cleanup(){foreach(var go in owned)if(go!=null)UnityEngine.Object.DestroyImmediate(go);owned.Clear();}
    }
}
