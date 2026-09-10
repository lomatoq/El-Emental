using System;
using System.Collections;
using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class RespawnProxyLeaseTests
    {
        [UnityTest] public IEnumerator OnlyOpaqueBodyMeshesAreCapturedAndLeased()
        {
            var actor=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.Destroy(actor.GetComponent<Collider>());
            var parent=new GameObject("Opaque body proxy fixture");
            var opaque=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var transparent=new Material(Shader.Find("Elemental/Light Dust Mote"));
            actor.GetComponent<Renderer>().sharedMaterial=opaque;
            var dust=GameObject.CreatePrimitive(PrimitiveType.Quad);dust.transform.SetParent(actor.transform,false);Object.Destroy(dust.GetComponent<Collider>());dust.GetComponent<Renderer>().sharedMaterial=transparent;
            var line=new GameObject("Actor line FX");line.transform.SetParent(actor.transform,false);line.AddComponent<LineRenderer>().sharedMaterial=opaque;
            Type type=typeof(GoldRespawnPresenter).Assembly.GetType("Elemental.Presentation.VFX.RespawnVisualProxy",true);
            object proxy=Activator.CreateInstance(type,true),gold=Activator.CreateInstance(type,true);
            try
            {
                var eligible=type.GetMethod("IsBodyRenderer");
                Assert.That(eligible.Invoke(null,new object[]{actor.GetComponent<Renderer>()}),Is.True,"Opaque shader contracts must remain visible to validation.");
                Assert.That(eligible.Invoke(null,new object[]{dust.GetComponent<Renderer>()}),Is.False);
                Assert.That(eligible.Invoke(null,new object[]{line.GetComponent<Renderer>()}),Is.False);
                type.GetMethod("Capture").Invoke(proxy,new object[]{actor.transform,parent.transform,Vector3.zero,true});
                type.GetMethod("RenderStage").Invoke(proxy,new object[]{Vector3.zero,Quaternion.identity,0f,Vector3.up,1u});
                var proxyRoot=(GameObject)type.GetProperty("Root").GetValue(proxy);
                Assert.That(proxyRoot.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(1));
                Assert.That(actor.GetComponent<Renderer>().forceRenderingOff,Is.True);
                Assert.That(dust.GetComponent<Renderer>().forceRenderingOff,Is.False);
                Assert.That(line.GetComponent<Renderer>().forceRenderingOff,Is.False);
                // URP Unlit lacks the gold emission contract: it must fail, not be silently filtered as FX.
                var error=Assert.Throws<System.Reflection.TargetInvocationException>(()=>type.GetMethod("Capture").Invoke(gold,new object[]{actor.transform,parent.transform,Vector3.zero,false}));
                Assert.That(error.InnerException,Is.TypeOf<InvalidOperationException>());
            }
            finally{((IDisposable)proxy).Dispose();((IDisposable)gold).Dispose();Object.Destroy(actor);Object.Destroy(parent);Object.Destroy(opaque);Object.Destroy(transparent);}
            yield return null;
        }
        [UnityTest] public IEnumerator OverlappingProxiesRestoreOnlyAfterLastReleaseInEitherOrder()
        {
            var actor=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.Destroy(actor.GetComponent<Collider>());
            var parent=new GameObject("Proxy lease fixture");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            actor.GetComponent<Renderer>().sharedMaterial=material;
            Type type=typeof(GoldRespawnPresenter).Assembly.GetType("Elemental.Presentation.VFX.RespawnVisualProxy",true);
            try
            {
                for(int initial=0;initial<2;initial++)for(int order=0;order<2;order++)
                {
                    var source=actor.GetComponent<Renderer>();source.forceRenderingOff=initial!=0;
                    object a=Activator.CreateInstance(type,true),b=Activator.CreateInstance(type,true);
                    try
                    {
                        foreach(object proxy in new[]{a,b})
                        {
                            type.GetMethod("Capture").Invoke(proxy,new object[]{actor.transform,parent.transform,Vector3.zero,true});
                            type.GetMethod("RenderStage").Invoke(proxy,new object[]{Vector3.zero,Quaternion.identity,0f,Vector3.up,1u});
                        }
                        Assert.That(source.forceRenderingOff,Is.True);
                        object first=order==0?a:b,last=order==0?b:a;
                        type.GetMethod("HideAndRestoreSource").Invoke(first,null);
                        Assert.That(source.forceRenderingOff,Is.True,"Remaining proxy must retain the renderer lease.");
                        type.GetMethod("HideAndRestoreSource").Invoke(first,null);
                        Assert.That(source.forceRenderingOff,Is.True,"Repeated cleanup must not release another owner's lease.");
                        type.GetMethod("HideAndRestoreSource").Invoke(last,null);
                        Assert.That(source.forceRenderingOff,Is.EqualTo(initial!=0));
                    }
                    finally{((IDisposable)a).Dispose();((IDisposable)b).Dispose();}
                    yield return null;
                }
            }
            finally{Object.Destroy(actor);Object.Destroy(parent);Object.Destroy(material);}
        }
    }
}
