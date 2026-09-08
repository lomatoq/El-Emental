using System.Collections;
using System.Collections.Generic;
using Elemental.Input.Actions;
using Elemental.Simulation.Networking;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class OnlineSemanticRoutingTests
    {
        [UnityTest]
        public IEnumerator HostHitchCoalescesContinuousFramesButRoutesPressAndReleaseSeparately()
        {
            var root = new GameObject("Remote semantic routing"); root.SetActive(false);
            try
            {
                root.AddComponent<PlayerInput>().enabled = false;
                var input = root.AddComponent<EarthInputAdapter>(); input.ConfigureRemoteInput(true);
                var frames = new List<EarthSemanticInputFrame>(); var routedAt = new List<int>();
                input.RemoteFrameApplied += frame => { frames.Add(frame); routedAt.Add(Time.frameCount); };
                root.SetActive(true);
                for (uint sequence = 1; sequence <= 5; sequence++)
                {
                    var frame = new EarthSemanticInputFrame { Sequence = sequence, Tick = sequence,
                        PointerViewport = new float2(.5f, .5f), CameraRotation = quaternion.identity,
                        FieldOfView = 70, Aspect = 16f / 9,
                        Pressed = sequence == 4 ? EarthInputBits.Jump : 0,
                        Released = sequence == 5 ? EarthInputBits.Jump : 0,
                        Held = sequence == 4 ? EarthInputBits.Jump : 0 };
                    Assert.That(input.EnqueueRemoteInput(frame), Is.True);
                }
                float deadline = Time.realtimeSinceStartup + .2f;
                while (frames.Count < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(frames.Count, Is.EqualTo(2));
                Assert.That(frames[0].Sequence, Is.EqualTo(4)); Assert.That(frames[0].Pressed, Is.EqualTo(EarthInputBits.Jump));
                Assert.That(frames[1].Released, Is.EqualTo(EarthInputBits.Jump));
                Assert.That(routedAt[1], Is.GreaterThan(routedAt[0]));
            }
            finally { Object.Destroy(root); }
        }
    }
}
