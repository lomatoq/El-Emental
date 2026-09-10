using System;
using System.Reflection;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishNativeInitializationTests
    {
        [TestCase(typeof(GoldRespawnPresenter))]
        [TestCase(typeof(MatchPresentationStage))]
        [TestCase(typeof(EarthMaterialFeedbackPresenter))]
        [TestCase(typeof(EarthMaterialFeedbackHub))]
        [TestCase(typeof(EarthMagicFeedback))]
        [TestCase(typeof(EarthAudioDirector))]
        public void AddSerializeToggleAndDestroyUnboundComponentIsSafe(Type type)
        {
            GameObject root=null;
            try
            {
                Assert.DoesNotThrow(()=>root=new GameObject("Native initialization regression"));
                root.SetActive(false);
                Component component=null;Assert.DoesNotThrow(()=>component=root.AddComponent(type));
                Assert.That(component,Is.Not.Null);
                string json=EditorJsonUtility.ToJson(component);
                Assert.DoesNotThrow(()=>EditorJsonUtility.FromJsonOverwrite(json,component));
                Assert.DoesNotThrow(()=>{root.SetActive(true);root.SetActive(false);});
                LogAssert.NoUnexpectedReceived();
            }
            finally{if(root!=null)Object.DestroyImmediate(root);}
            LogAssert.NoUnexpectedReceived();
        }
        [Test]
        public void GoldNativeSlotsInitializeOnceWithoutConstructingEffectsOrLeases()
        {
            var root=new GameObject("Gold native slot regression");root.SetActive(false);
            try
            {
                var gold=root.AddComponent<GoldRespawnPresenter>();
                var slots=(Array)typeof(GoldRespawnPresenter).GetField("_slots",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gold);
                var field=slots.GetValue(0).GetType().GetField("RingProperties",BindingFlags.Instance|BindingFlags.Public);
                foreach(var slot in slots)Assert.That(field.GetValue(slot),Is.Null,"No native wrapper may be allocated by field construction or serialization.");
                var initialize=typeof(GoldRespawnPresenter).GetMethod("InitializeSlotProperties",BindingFlags.Instance|BindingFlags.NonPublic);
                initialize.Invoke(gold,null);
                var first=(MaterialPropertyBlock)field.GetValue(slots.GetValue(0));var second=(MaterialPropertyBlock)field.GetValue(slots.GetValue(1));
                Assert.That(first,Is.Not.Null);Assert.That(second,Is.Not.Null);Assert.That(first,Is.Not.SameAs(second));
                first.SetFloat("_NativeInitializationSentinel",.625f);
                initialize.Invoke(gold,null);
                Assert.That(field.GetValue(slots.GetValue(0)),Is.SameAs(first));Assert.That(field.GetValue(slots.GetValue(1)),Is.SameAs(second));
                Assert.That(first.GetFloat("_NativeInitializationSentinel"),Is.EqualTo(.625f));
                Assert.That(gold.OwnedRoot,Is.Null);Assert.That(gold.StandingPosesReady,Is.False);Assert.That(gold.VisibleProxyCount,Is.Zero);
            }
            finally{Object.DestroyImmediate(root);}
            LogAssert.NoUnexpectedReceived();
        }
    }
}
