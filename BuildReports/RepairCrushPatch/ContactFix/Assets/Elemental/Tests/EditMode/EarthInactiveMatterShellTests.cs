using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthInactiveMatterShellTests
    {
        [Test]
        public void DestroyedPreviousKernelMustNotAliasNewRegistryFirstHandle()
        {
            var oldObject = new GameObject("Old matter world");
            var newObject = new GameObject("New matter world");
            var shell = new GameObject("Surviving pooled shell");
            var unrelated = new GameObject("Unrelated new-world matter");
            try
            {
                var oldKernel = oldObject.AddComponent<EarthMatterKernelBehaviour>();
                var identity = shell.AddComponent<EarthMatterIdentity>();
                EarthMatterRecord authored = Record();
                Assert.That(identity.Configure(oldKernel, authored), Is.True);
                EarthMatterId previous = identity.MatterId;
                Object.DestroyImmediate(oldObject);
                Assert.That(oldKernel == null, Is.True, "Reproduce Unity fake-null destroyed owner.");
                var newKernel = newObject.AddComponent<EarthMatterKernelBehaviour>();
                var other = unrelated.AddComponent<EarthMatterIdentity>();
                Assert.That(other.Configure(newKernel, authored), Is.True);
                Assert.That(other.MatterId, Is.EqualTo(previous), "Registries independently issue the same local handle.");
                Assert.That(identity.Configure(newKernel, authored), Is.True);
                Assert.That(identity.MatterId, Is.Not.EqualTo(other.MatterId));
                Assert.That(newKernel.ActiveRecordCount, Is.EqualTo(2));
                Assert.That(other.TryRead(out var preserved), Is.True);
                Assert.That(preserved.Mass, Is.EqualTo(authored.Mass));
            }
            finally
            {
                Object.DestroyImmediate(oldObject); Object.DestroyImmediate(newObject);
                Object.DestroyImmediate(shell); Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void InactiveShellWithLiveMatterCannotBeClaimedUntilTransientRetirement()
        {
            var host = new GameObject("Matter world");
            var shell = new GameObject("Inactive quick stone shell");
            try
            {
                var kernel = host.AddComponent<EarthMatterKernelBehaviour>();
                var identity = shell.AddComponent<EarthMatterIdentity>();
                var fragment = shell.AddComponent<EarthFragment>();
                EarthMatterRecord authored = Record();
                Assert.That(identity.Configure(kernel, authored), Is.True);
                shell.SetActive(false);
                Assert.That(fragment.CanReuseInactiveRepresentation, Is.False);
                Assert.That(identity.TryRead(out var preserved), Is.True);
                Assert.That(preserved.Mass, Is.EqualTo(authored.Mass));
                Assert.That(identity.RetireTransientRepresentation(), Is.True);
                Assert.That(fragment.CanReuseInactiveRepresentation, Is.True);
                Assert.That(identity.Configure(kernel, authored), Is.True, "Recycled transient shell must register without None error.");
                Assert.That(fragment.CanReuseInactiveRepresentation, Is.False);
            }
            finally { Object.DestroyImmediate(shell); Object.DestroyImmediate(host); }
        }

        private static EarthMatterRecord Record() => new()
        {
            Phase = EarthMatterPhase.Forming,
            Representation = EarthRepresentationTier.HeroPhysical,
            Material = EarthMaterialKind.Stone,
            Shape = EarthShapeSemantic.NaturalRock,
            Volume = .1f, Mass = 12f, Integrity = 1f,
            RestPose = new EarthMatterPose(float3.zero, quaternion.identity),
            CurrentPose = new EarthMatterPose(float3.zero, quaternion.identity),
            Source = new EarthSourceProvenance(EarthSourceKind.Fragment, 41u, 1, -1, 1u,
                float3.zero, .1f, EarthProvenanceFlags.VolumeReserved)
        };
    }
}
