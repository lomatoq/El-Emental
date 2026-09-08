using System.Collections;
using Elemental.Runtime.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class FireEnvironmentRuntimeTests
    {
        [UnityTest] public IEnumerator SweepsAndInitialOverlapUseBoundedQueriesAndFiniteContacts()
        {
            var wall = new GameObject("Fire runtime test wall");
            var query = new GameObject("Fire runtime query");
            try
            {
                wall.layer = 30;
                var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(6, 6, 0.2f);
                wall.AddComponent<FireSurfaceBinding>().Configure(991);
                var sphere = query.AddComponent<SphereCollider>(); sphere.enabled = false;
                var resolver = new FireSurfaceResolver(); var cache = new FireContactCache();
                var environment = new FireEnvironmentAdapter(resolver, sphere, 1 << 30);
                var nodes = new[] { FireFieldNode.Stream(new float3(0, 0, -2), new float3(0, 0, 2), new float3(0, 0, 12), new float3(0, 1, 0)) };
                UnityEngine.Physics.SyncTransforms();
                environment.Collect(nodes, 1, 1f / 60, 0, cache);
                Assert.That(environment.QueryCount, Is.LessThanOrEqualTo(12));
                Assert.That(environment.ProbeCount, Is.LessThanOrEqualTo(6));
                Assert.That(nodes[0].B.z, Is.LessThan(0));
                var patches = new FireContactPatch[8]; Assert.That(cache.CopyCurrent(resolver, 0, patches), Is.GreaterThan(0));
                nodes[0] = FireFieldNode.Stream(new float3(0, 0, -0.11f), new float3(0, 0, 1), new float3(0, 0, 12), new float3(0, 1, 0));
                environment.Collect(nodes, 1, 1f / 60, 0, cache);
                Assert.That(environment.InitialOverlaps, Is.GreaterThan(0));
                Assert.That(environment.QueryCount, Is.LessThanOrEqualTo(12));
                wall.SetActive(false);
                Assert.That(cache.CopyCurrent(resolver, 0, patches), Is.Zero);
                yield return null;
            }
            finally { Object.Destroy(wall); Object.Destroy(query); }
        }
    }
}
