using System.Collections;
using Elemental.Runtime.Matter;
using Elemental.Runtime.World;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthMatterReturnRuntimeTests
    {
        [UnityTest]
        public IEnumerator MultiBodyReturnKeepsRepresentationsUntilBothVoxelCommits()
        {
            GameObject planetObject = new GameObject("Return Transaction Planet");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 90210u, 8, 1f, 1, 1, null);
            EarthMatterKernelBehaviour kernel = planetObject.AddComponent<EarthMatterKernelBehaviour>();
            EarthMatterReturnController controller = planetObject.AddComponent<EarthMatterReturnController>();
            planetObject.SetActive(true);
            controller.Configure(planet, kernel, 120f);
            for (int frame = 0; frame < 100 &&
                 (planet.PendingRenderCount > 0 || planet.PendingColliderCount > 0); frame++)
                yield return null;

            var identities = new EarthMatterIdentity[2];
            var ids = new EarthMatterId[2];
            var bodies = new GameObject[2];
            for (int index = 0; index < bodies.Length; index++)
            {
                Vector3 source = new Vector3((index == 0 ? -1f : 1f) * 0.35f, 8.05f, 0f);
                GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyObject.name = $"Returning Matter {index}";
                bodyObject.transform.position = source;
                bodyObject.transform.localScale = Vector3.one * 0.35f;
                Rigidbody body = bodyObject.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.mass = 5.145f;
                float volume = body.mass / 120f;
                var provenance = new EarthSourceProvenance(
                    EarthSourceKind.TerrainEdit,
                    1u,
                    1,
                    index,
                    7u,
                    new float3(source.x, source.y, source.z),
                    volume,
                    EarthProvenanceFlags.ExactReturnSupported |
                    EarthProvenanceFlags.SourceCavityValid |
                    EarthProvenanceFlags.VolumeReserved);
                identities[index] = EarthMatterRuntimeBridge.EnsureIdentity(
                    body,
                    kernel,
                    body,
                    EarthMatterPhase.FreeDynamic,
                    EarthRepresentationTier.HeroPhysical,
                    EarthMaterialKind.Stone,
                    EarthShapeSemantic.Debris,
                    volume,
                    body.mass,
                    provenance);
                Assert.That(identities[index], Is.Not.Null);
                ids[index] = identities[index].MatterId;
                bodies[index] = bodyObject;
            }

            Assert.That(controller.TryBeginReturnsNonAlloc(identities, identities.Length, Vector3.up * 8f),
                Is.EqualTo(2));
            yield return new WaitForFixedUpdate();

            Assert.That(controller.ActiveReturnCount, Is.EqualTo(2));
            Assert.That(planet.PendingEditTransactionCount, Is.EqualTo(2));
            for (int index = 0; index < bodies.Length; index++)
            {
                Assert.That(bodies[index].activeSelf, Is.True,
                    "A physical representation may not disappear before its own SDF receipt commits.");
                Assert.That(identities[index].TryRead(out EarthMatterRecord pending), Is.True);
                Assert.That(pending.Phase, Is.EqualTo(EarthMatterPhase.Reintegrating));
            }

            for (int frame = 0; frame < 120 && controller.IsReturning; frame++) yield return null;
            Assert.That(controller.ActiveReturnCount, Is.Zero);
            Assert.That(planet.PendingEditTransactionCount, Is.Zero);
            for (int index = 0; index < bodies.Length; index++)
            {
                Assert.That(bodies[index].activeSelf, Is.False);
                Assert.That(kernel.TryGet(ids[index], out EarthMatterRecord committed), Is.True);
                Assert.That(committed.Phase, Is.EqualTo(EarthMatterPhase.TerrainAttached));
                Assert.That(committed.Representation, Is.EqualTo(EarthRepresentationTier.CanonicalTerrain));
                Object.Destroy(bodies[index]);
            }

            Object.Destroy(planetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator JammedReturnSettlesInPlaceInsteadOfTeleportingOrDisappearing()
        {
            GameObject planetObject = new GameObject("Jammed Return Planet");
            planetObject.SetActive(false);
            VoxelPlanetBehaviour planet = planetObject.AddComponent<VoxelPlanetBehaviour>();
            planet.Configure(8f, 7755u, 8, 1f, 1, 1, null);
            EarthMatterKernelBehaviour kernel = planetObject.AddComponent<EarthMatterKernelBehaviour>();
            EarthMatterReturnController controller = planetObject.AddComponent<EarthMatterReturnController>();
            planetObject.SetActive(true);
            controller.Configure(planet, kernel, 120f);

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.transform.position = new Vector3(0f, 10f, 0f);
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            const float volume = 0.1f;
            var provenance = new EarthSourceProvenance(
                EarthSourceKind.TerrainEdit,
                1u,
                1,
                0,
                1u,
                new float3(0f, 8f, 0f),
                volume,
                EarthProvenanceFlags.ExactReturnSupported |
                EarthProvenanceFlags.SourceCavityValid |
                EarthProvenanceFlags.VolumeReserved);
            EarthMatterIdentity identity = EarthMatterRuntimeBridge.EnsureIdentity(
                body,
                kernel,
                body,
                EarthMatterPhase.FreeDynamic,
                EarthRepresentationTier.HeroPhysical,
                EarthMaterialKind.Stone,
                EarthShapeSemantic.Debris,
                volume,
                12f,
                provenance);
            Vector3 start = body.position;

            Assert.That(controller.TryBeginReturn(identity, Vector3.up * 8f), Is.True);
            for (int tick = 0; tick < 60 && controller.IsReturning; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(controller.IsReturning, Is.False);
            Assert.That(bodyObject.activeSelf, Is.True,
                "A jam produces a visible settle pile, not a hidden representation.");
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.001f),
                "The jam fallback must not teleport the body to its destination.");
            Assert.That(identity.TryRead(out EarthMatterRecord record), Is.True);
            Assert.That(record.Phase, Is.EqualTo(EarthMatterPhase.FreeDynamic));
            Assert.That(planet.State.EditCount, Is.Zero);

            Object.Destroy(bodyObject);
            Object.Destroy(planetObject);
            yield return null;
        }
    }
}
