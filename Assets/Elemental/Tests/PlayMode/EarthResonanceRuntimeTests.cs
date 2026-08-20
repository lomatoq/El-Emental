using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthResonanceRuntimeTests
    {
        [UnityTest]
        public IEnumerator ChargedResonanceBuildsUpperHemisphereAndFiresWithoutCasterRecoil()
        {
            GameObject root = new GameObject("Resonance Runtime Root");
            root.SetActive(false);
            EarthFragmentPool pool = root.AddComponent<EarthFragmentPool>();
            Mesh shape = BuildCubeMesh();
            pool.Configure(28, null, null, shape);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            GameObject casterObject = new GameObject("Resonance Runtime Caster");
            casterObject.transform.position = Vector3.up * 24f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            CapsuleCollider casterCollider = casterObject.AddComponent<CapsuleCollider>();
            EarthResonanceController resonance = casterObject.AddComponent<EarthResonanceController>();
            resonance.Configure(caster, null, root.transform, pool, executor, null);
            root.SetActive(true);

            float now = Time.fixedUnscaledTime;
            Assert.That(resonance.BeginCharge(now), Is.True);
            resonance.ContinueCharge(now + 1.55f, Vector3.forward);
            Assert.That(resonance.ActiveStoneCount, Is.InRange(8, 28));
            Assert.That(resonance.ReleaseCharge(now + 1.55f, Vector3.forward), Is.True);
            for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();

            int hovering = 0;
            int aboveWaist = 0;
            float largestVisualAxis = 0f;
            float minimumHorizontal = float.PositiveInfinity;
            float maximumHorizontal = 0f;
            EarthFragment[] fragments = root.GetComponentsInChildren<EarthFragment>(true);
            for (int index = 0; index < fragments.Length; index++)
            {
                EarthFragment fragment = fragments[index];
                if (!fragment.gameObject.activeSelf ||
                    fragment.TargetKind != EarthPhysicalTargetKind.ResonanceProjectile) continue;
                hovering++;
                if (fragment.Body.worldCenterOfMass.y > caster.worldCenterOfMass.y - 0.1f) aboveWaist++;
                float horizontal = Vector3.ProjectOnPlane(
                    fragment.Body.worldCenterOfMass - caster.worldCenterOfMass,
                    Vector3.up).magnitude;
                minimumHorizontal = Mathf.Min(minimumHorizontal, horizontal);
                maximumHorizontal = Mathf.Max(maximumHorizontal, horizontal);
                largestVisualAxis = Mathf.Max(largestVisualAxis,
                    fragment.transform.localScale.x,
                    fragment.transform.localScale.y,
                    fragment.transform.localScale.z);
                Assert.That(fragment.gameObject.layer, Is.EqualTo(2),
                    "Held resonance stones stay physical but must be soft obstacles for the camera.");
            }
            Assert.That(hovering, Is.GreaterThanOrEqualTo(8));
            Assert.That(aboveWaist, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(hovering * 0.75f)),
                "Resonance should read as an upper hemisphere, not a cylinder around the feet.");
            Assert.That(maximumHorizontal - minimumHorizontal, Is.GreaterThan(1.0f),
                "The spell needs a filled dome gradient rather than one cylindrical ring.");
            Assert.That(largestVisualAxis, Is.GreaterThan(0.82f),
                "Resonance stones need to read as substantial earth ammunition.");

            Vector3 casterVelocity = caster.linearVelocity;
            int fired = resonance.FireAll(Vector3.forward);
            Assert.That(fired, Is.EqualTo(hovering));
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(caster.linearVelocity, casterVelocity), Is.LessThan(0.01f),
                "Owned resonance projectiles must not recoil-launch the caster.");
            for (int index = 0; index < fragments.Length; index++)
                if (fragments[index].gameObject.activeSelf)
                    Assert.That(fragments[index].gameObject.layer, Is.EqualTo(0));

            Object.Destroy(casterObject);
            Object.Destroy(root);
            Object.Destroy(shape);
            yield return null;
        }

        private static Mesh BuildCubeMesh()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "Resonance Test Beveled Placeholder";
            Object.DestroyImmediate(cube);
            return mesh;
        }

    }
}
