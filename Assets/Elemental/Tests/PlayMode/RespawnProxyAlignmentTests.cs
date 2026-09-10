using System;
using System.Collections;
using Elemental.Presentation.VFX;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class RespawnProxyAlignmentTests
    {
        [UnityTest] public IEnumerator FinalProxyVerticesMatchRelocatedActorAtCanonicalRoot()
        {
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var parent = new GameObject("Respawn alignment fixture");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            actor.GetComponent<Renderer>().sharedMaterial = material;
            actor.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(23, 67, 11));
            actor.transform.localScale = new Vector3(.8f, 1.4f, .9f);
            Vector3 cachedFeet = actor.transform.TransformPoint(new Vector3(.04f, -.9f, .03f));
            Type type = typeof(GoldRespawnPresenter).Assembly.GetType("Elemental.Presentation.VFX.RespawnVisualProxy", true);
            object proxy = Activator.CreateInstance(type, true);
            try
            {
                type.GetMethod("Capture").Invoke(proxy, new object[]{actor.transform, parent.transform, cachedFeet, true});
                Vector3 destination = new Vector3(-7, 2.025f, 14);
                Quaternion rotation = Quaternion.Euler(-17, 125, 35);
                type.GetMethod("RenderAtActorPose").Invoke(proxy, new object[]{destination, rotation, 1f, 0f, rotation * Vector3.up, Color.black});
                actor.transform.SetPositionAndRotation(destination, rotation);
                var root = (GameObject)type.GetProperty("Root").GetValue(proxy);
                MeshFilter copied = root.GetComponentInChildren<MeshFilter>();
                MeshFilter source = actor.GetComponent<MeshFilter>();
                foreach(Vector3 vertex in source.sharedMesh.vertices)
                    Assert.That(Vector3.Distance(copied.transform.TransformPoint(vertex), source.transform.TransformPoint(vertex)), Is.LessThan(.00001f), "Final reveal geometry must agree with the relocated live actor including cached scale and pivot.");
                Assert.That(actor.transform.localScale, Is.EqualTo(new Vector3(.8f, 1.4f, .9f)));
            }
            finally { ((IDisposable)proxy).Dispose(); Object.Destroy(actor); Object.Destroy(parent); Object.Destroy(material); }
            yield return null;
        }
    }
}
