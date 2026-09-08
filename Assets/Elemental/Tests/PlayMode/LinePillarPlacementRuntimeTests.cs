using System.Collections;
using System.Reflection;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class LinePillarPlacementRuntimeTests
    {
        [UnityTest]
        public IEnumerator GroundOffsetMovesCrestPhysicsAndRenderWithoutResizingOrMovingOrdinaryWaves()
        {
            var root = new GameObject("Line placement test");
            var baseline = ScriptableObject.CreateInstance<EarthPillarWaveProfile>();
            var shifted = ScriptableObject.CreateInstance<EarthPillarWaveProfile>();
            typeof(EarthPillarWaveProfile).GetField("lineGroundOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(shifted, -.8f);
            try
            {
                var a = Create(root.transform, baseline, EarthCharacterImpactSourceKind.PillarCrest);
                var b = Create(root.transform, shifted, EarthCharacterImpactSourceKind.PillarCrest);
                var ordinary = Create(root.transform, shifted, EarthCharacterImpactSourceKind.PillarWave);
                int samples = 0;
                for (int frame = 0; frame < 16; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    if (!a.TryGetVisiblePlacementDiagnostic(out _, out var ma, out var ground, out _, out _, out _)) continue;
                    Assert.That(b.TryGetVisiblePlacementDiagnostic(out _, out var mb, out var shiftedGround, out _, out _, out _), Is.True);
                    Assert.That(ordinary.TryGetVisiblePlacementDiagnostic(out _, out var mc, out _, out _, out _, out _), Is.True);
                    Assert.That(b.Body.position.y - a.Body.position.y, Is.EqualTo(-.8f).Within(.005f));
                    Assert.That(mb.m13 - ma.m13, Is.EqualTo(-.8f).Within(.005f));
                    Assert.That(mc.m13 - ma.m13, Is.EqualTo(0f).Within(.005f));
                    Assert.That((b.transform.localScale - a.transform.localScale).magnitude, Is.LessThan(.0001f));
                    Assert.That(shiftedGround, Is.EqualTo(ground), "Dust/contact keep the real terrain plane.");
                    samples++;
                }
                Assert.That(samples, Is.GreaterThan(5));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(baseline);
                Object.Destroy(shifted);
            }
        }

        private static EarthPillarWaveColumn Create(Transform root, EarthPillarWaveProfile profile,
            EarthCharacterImpactSourceKind kind)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.SetParent(root);
            obj.AddComponent<Rigidbody>().useGravity = false;
            var column = obj.AddComponent<EarthPillarWaveColumn>();
            column.Schedule(null, Vector3.up * 100f, Vector3.up, Vector3.forward,
                3.15f, 1f, 1f, 0f, .5f, 1f, 12001, 0f, null, profile, new Collider[8], impactKind: kind);
            return column;
        }
    }
}
