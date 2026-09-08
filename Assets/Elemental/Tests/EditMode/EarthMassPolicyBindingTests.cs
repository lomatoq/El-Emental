using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMassPolicyBindingTests
    {
        [Test]
        public void EditableAssetIsCapturedPerWorldAndDoesNotRepriceExistingWorld()
        {
            var asset = ScriptableObject.CreateInstance<EarthMatterMassPolicyAsset>();
            var first = new GameObject("First mass world");
            var second = new GameObject("Second mass world");
            try
            {
                var a = first.AddComponent<EarthMatterKernelBehaviour>(); a.ConfigureMassPolicy(asset);
                EarthMatterMassProfile original = a.MassPolicy;
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("referenceGameplayMassKilograms").floatValue = 240f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var b = second.AddComponent<EarthMatterKernelBehaviour>(); b.ConfigureMassPolicy(asset);
                Assert.That(a.MassPolicy.ReferenceGameplayMassKilograms, Is.EqualTo(120f));
                Assert.That(b.MassPolicy.ReferenceGameplayMassKilograms, Is.EqualTo(240f));
                Assert.That(original.ReferenceGameplayMassKilograms, Is.EqualTo(120f));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ManyAccretionDeliveriesEqualOneDeliveryWithoutRepeatedMinimumMass()
        {
            EarthMatterMassProfile policy = EarthMatterMassProfile.ArenaStone;
            float volume = .004f;
            float mass = EarthMatterMassPolicy.ResolveGameplayMass(volume, in policy);
            for (int i = 0; i < 100; i++)
            {
                mass += EarthMatterMassPolicy.AccretedMass(volume, .0001f, in policy);
                volume += .0001f;
            }
            Assert.That(mass, Is.EqualTo(EarthMatterMassPolicy.ResolveGameplayMass(.014f, in policy)).Within(.001f));
        }

        [Test]
        public void NormalizedParentSplitsAndRepairsWithoutRenormalizingChildren()
        {
            EarthMatterMassProfile policy = EarthMatterMassProfile.ArenaStone;
            var registry = new EarthMatterRegistry(32);
            EarthMatterRecord parent = Record(.2f, EarthMatterMassPolicy.ResolveGameplayMass(.2f, in policy));
            Assert.That(registry.TryRegister(parent, out EarthMatterId id), Is.True);
            var children = new EarthMatterRecord[4];
            var ids = new EarthMatterId[4];
            for (int i = 0; i < 4; i++) children[i] = Record(.05f, EarthMatterMassPolicy.ResolveGameplayMass(.05f, in policy));
            Assert.That(registry.TrySplit(id, children, 4, ids), Is.False, "Re-normalizing every child creates extra mass.");
            for (int i = 0; i < 4; i++) children[i].Mass = parent.Mass / 4f;
            Assert.That(registry.TrySplit(id, children, 4, ids), Is.True);
            Assert.That(registry.TryMerge(id, ids, 4, parent, out EarthMatterId restored), Is.True);
            Assert.That(registry.TryGet(restored, out EarthMatterRecord record), Is.True);
            Assert.That(record.Mass, Is.EqualTo(parent.Mass).Within(.001f));
            Assert.That(record.Volume, Is.EqualTo(parent.Volume).Within(.00001f));
        }

        [Test]
        public void AccretionUpdatesCanonicalVolumeAndMassAndRejectsInvalidInputs()
        {
            var registry = new EarthMatterRegistry(32);
            EarthMatterRecord parent = Record(.1f, 120f);
            Assert.That(registry.TryRegister(parent, out EarthMatterId id), Is.True);
            Assert.That(registry.TryAccreteTerrain(id, .05f, 30f), Is.True);
            Assert.That(registry.TryAccreteTerrain(id, float.NaN, 30f), Is.False);
            Assert.That(registry.TryGet(id, out EarthMatterRecord grown), Is.True);
            Assert.That(grown.Volume, Is.EqualTo(.15f).Within(.00001f));
            Assert.That(grown.Mass, Is.EqualTo(150f));
            Assert.That(grown.Source.ReservedVolume, Is.EqualTo(.15f).Within(.00001f));
        }

        private static EarthMatterRecord Record(float volume, float mass) => new()
        {
            Phase = EarthMatterPhase.FreeDynamic, Representation = EarthRepresentationTier.HeroPhysical,
            Material = EarthMaterialKind.Stone, Shape = EarthShapeSemantic.NaturalRock,
            Volume = volume, Mass = mass, Integrity = 1f,
            RestPose = EarthMatterPose.Identity, CurrentPose = EarthMatterPose.Identity,
            Source = new EarthSourceProvenance(EarthSourceKind.TerrainEdit, 1, 1, -1, 1,
                float3.zero, volume, EarthProvenanceFlags.VolumeReserved)
        };
    }
}
