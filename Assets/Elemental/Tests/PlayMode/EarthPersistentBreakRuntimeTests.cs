using System.Collections;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPersistentBreakRuntimeTests
    {
        [UnityTest]
        public IEnumerator SplitChildrenKeepMassAndTargetabilityBeyondCosmeticLifetime()
        {
            GameObject host = new GameObject("Persistent split test");
            GameObject poolHost = new GameObject("Persistent split pool");
            poolHost.SetActive(false);
            GameObject parent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parent.transform.position = new Vector3(0, 1000, 0);
            Rigidbody body = parent.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 48f;
            EarthMatterIdentity identity = parent.AddComponent<EarthMatterIdentity>();
            EarthMatterKernelBehaviour kernel = host.AddComponent<EarthMatterKernelBehaviour>();
            EarthMaterialFeedbackHub hub = host.AddComponent<EarthMaterialFeedbackHub>();
            EarthMaterialFeedbackCue lastCue = default;
            int cueCount = 0;
            hub.Presented += cue => { lastCue = cue; cueCount++; };
            EarthRockDebrisPool pool = poolHost.AddComponent<EarthRockDebrisPool>();
            pool.Configure(16, null, parent.GetComponent<MeshFilter>().sharedMesh, null, null);
            pool.ConfigureMatterKernel(kernel);
            pool.ConfigureMaterialFeedback(hub);
            poolHost.SetActive(true);
            try
            {
                EarthRockBreakDecision decision = EarthRockBreakPolicy.Resolve(0.8f, 48f, 480f, false);
                Assert.That(pool.TryEmitBreak(parent.transform.position, Vector3.up, Vector3.zero,
                    0.8f, 48f, 123, decision, 0, identity), Is.True, pool.LastBreakRejection);
                hub.FlushPending();
                Assert.That(cueCount, Is.EqualTo(1));
                Assert.That(lastCue.Kind, Is.EqualTo(EarthMaterialFeedbackKind.Fracture));
                Assert.That(lastCue.SourceId, Is.EqualTo(123u));
                Assert.That(lastCue.DustCount, Is.EqualTo(64));
                Assert.That(lastCue.ChipCount, Is.EqualTo(16));
                EarthMatterId parentId = identity.MatterId;
                int activeRecords = kernel.ActiveRecordCount;
                Assert.That(pool.TryEmitBreak(parent.transform.position, Vector3.up, Vector3.zero,
                    0.8f, 48f, 123, decision, 0, identity), Is.False, "Duplicate parent split must not mint mass");
                Assert.That(kernel.ActiveRecordCount, Is.EqualTo(activeRecords));
                Assert.That(kernel.TryGet(parentId, out EarthMatterRecord consumed), Is.True);
                Assert.That(consumed.Phase, Is.EqualTo(EarthMatterPhase.Consumed));
                hub.FlushPending(); // Flush the rejected duplicate's separate impact cue.
                parent.SetActive(false);
                EarthRockDebris[] medium = pool.GetComponentsInChildren<EarthRockDebris>(false);
                Assert.That(medium.Length, Is.EqualTo(4));

                // Reuse the retired authored projectile shell as a distinct huge stone.
                Assert.That(identity.ReleaseRetiredRepresentation(), Is.True);
                parent.transform.position += Vector3.right * 100f;
                body.mass = 96f;
                parent.SetActive(true);
                EarthRockBreakDecision hugeDecision = EarthRockBreakPolicy.Resolve(2f, 96f, 960f, false);
                int previousCues = cueCount;
                Assert.That(pool.TryEmitBreak(parent.transform.position, Vector3.up, Vector3.zero,
                    2f, 96f, 456, hugeDecision, 0, identity), Is.True, pool.LastBreakRejection);
                hub.FlushPending();
                Assert.That(cueCount, Is.EqualTo(previousCues + 1));
                Assert.That(lastCue.Kind, Is.EqualTo(EarthMaterialFeedbackKind.Fracture));
                Assert.That(lastCue.SourceId, Is.EqualTo(456u));
                Assert.That(lastCue.DustCount, Is.EqualTo(140));
                Assert.That(lastCue.ChipCount, Is.EqualTo(28));
                parent.SetActive(false);
                EarthRockDebris[] chunks = pool.GetComponentsInChildren<EarthRockDebris>(false);
                Assert.That(chunks.Length, Is.EqualTo(7), "4 medium + 3 huge physical children");
                float mass = 0f;
                int mediumCount = 0, hugeCount = 0;
                float mediumMass = 0f, hugeMass = 0f;
                foreach (EarthRockDebris chunk in chunks)
                {
                    mass += chunk.EarthMass;
                    chunk.Body.detectCollisions = false; // Isolate lifecycle/ownership from scene geometry.
                    Assert.That(chunk.IsEarthTargetValid, Is.True);
                    chunk.OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
                    Assert.That(chunk.MatterIdentity.TryRead(out EarthMatterRecord held), Is.True);
                    Assert.That(held.Phase, Is.EqualTo(EarthMatterPhase.Controlled));
                    if (held.Source.SourceStableId == 123u) { mediumCount++; mediumMass += held.Mass; }
                    if (held.Source.SourceStableId == 456u) { hugeCount++; hugeMass += held.Mass; }
                    chunk.OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
                }
                Assert.That(mediumCount, Is.EqualTo(4));
                Assert.That(hugeCount, Is.EqualTo(3));
                Assert.That(mediumMass, Is.EqualTo(48f).Within(0.001f));
                Assert.That(hugeMass, Is.EqualTo(96f).Within(0.001f));
                Assert.That(mass, Is.EqualTo(144f).Within(0.001f));
                yield return new WaitForSeconds(2.3f);
                foreach (EarthRockDebris chunk in chunks)
                {
                    Assert.That(chunk.gameObject.activeSelf && chunk.IsEarthTargetValid, Is.True);
                    chunk.ResetPiece();
                    Assert.That(chunk.gameObject.activeSelf, Is.True, "Live canonical mass cannot be pooled by a cosmetic reset");
                }
                // Fill the remaining 9 physical slots with three legitimate huge splits.
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(identity.ReleaseRetiredRepresentation(), Is.True);
                    parent.transform.position += Vector3.right * 100f;
                    parent.SetActive(true);
                    Assert.That(pool.TryEmitBreak(parent.transform.position, Vector3.up, Vector3.zero,
                        2f, 96f, (uint)(600 + i), hugeDecision, 0, identity), Is.True, pool.LastBreakRejection);
                    parent.SetActive(false);
                }
                Assert.That(pool.GetComponentsInChildren<EarthRockDebris>(false).Length, Is.EqualTo(16));
                EarthRockDebris retained = chunks[0];
                EarthMatterId retainedId = retained.MatterIdentity.MatterId;
                float retainedMass = retained.EarthMass;
                Assert.That(pool.TryEmitBreak(retained.transform.position, Vector3.up, Vector3.zero,
                    0.8f, retainedMass, retained.StableEarthId, decision, 1, retained.MatterIdentity), Is.False);
                Assert.That(pool.LastBreakRejection, Does.StartWith("PhysicalPoolFull"));
                Assert.That(retained.gameObject.activeSelf && retained.IsEarthTargetValid, Is.True);
                Assert.That(retained.MatterIdentity.MatterId, Is.EqualTo(retainedId));
                Assert.That(retained.EarthMass, Is.EqualTo(retainedMass));
            }
            finally
            {
                Object.Destroy(parent);
                Object.Destroy(poolHost);
                Object.Destroy(host);
            }
        }
    }
}
