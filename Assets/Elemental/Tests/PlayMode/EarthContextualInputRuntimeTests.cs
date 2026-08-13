using System.Collections;
using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthContextualInputRuntimeTests
    {
        [UnityTest]
        public IEnumerator InvalidStrokeDoesNotCreateAuthoritativeCommand()
        {
            GameObject planetObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planetObject.name = "Task5 Input Planet";
            SphereCollider planetCollider = planetObject.GetComponent<SphereCollider>();
            planetCollider.radius = 12f;

            GameObject cameraObject = new GameObject("Task5 Input Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            camera.transform.position = new Vector3(0f, 0f, -24f);
            camera.transform.LookAt(Vector3.zero);

            GameObject executorObject = new GameObject("Task5 Input Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();

            GameObject inputObject = new GameObject("Task5 Input Controller");
            inputObject.SetActive(false);
            MagicInputController input = inputObject.AddComponent<MagicInputController>();
            input.Configure(null, camera, executor, planetCollider, null);
            var tooShort = new List<float2>
            {
                new float2(400f, 300f),
                new float2(401f, 300f)
            };

            bool executed = input.TryCommitScreenPath(tooShort, 0.5f);
            Assert.That(executed, Is.False);
            Assert.That(executor.SuccessfulCommandCount, Is.Zero);
            Assert.That(input.ReticleState, Is.EqualTo(EarthReticleState.Invalid));

            Object.Destroy(planetObject);
            Object.Destroy(cameraObject);
            Object.Destroy(executorObject);
            Object.Destroy(inputObject);
            yield return null;
        }
    }
}
