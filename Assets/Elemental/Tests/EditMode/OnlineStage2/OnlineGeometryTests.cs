using System;
using Elemental.Online;
using Elemental.Runtime.Physics;
using Unity.Collections;
using Unity.Netcode;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class OnlineGeometryTests
    {
        [TestCase(EarthPhysicsBodyClass.LightStone)]
        [TestCase(EarthPhysicsBodyClass.HeavyBlock)]
        [TestCase(EarthPhysicsBodyClass.Structure)]
        public void GeneratedEarthContactMaterialSurvivesRealWireWithoutAnAssetId(EarthPhysicsBodyClass bodyClass)
        {
            var profile = ScriptableObject.CreateInstance<EarthPhysicsFeelProfile>();
            var owner = new GameObject("Runtime Earth contact"); PhysicsMaterial source = null, replica = null;
            try
            {
                var collider = owner.AddComponent<BoxCollider>(); profile.Apply(null, collider, bodyClass); source = collider.sharedMaterial;
                var packet = new OnlinePacket { Kind = OnlineMessage.BodySpawn, Flags = 2u | (1u << 16), Seed = 0 };
                OnlineCollisionMaterialState.Capture(source, ref packet);
                using var writer = new FastBufferWriter(OnlinePacket.MaximumBytes, Allocator.Temp); writer.WriteValueSafe(packet);
                using var reader = new FastBufferReader(writer, Allocator.Temp); reader.ReadValueSafe(out OnlinePacket wire);
                replica = OnlineCollisionMaterialState.Create(wire);
                Assert.That(replica.dynamicFriction, Is.EqualTo(source.dynamicFriction));
                Assert.That(replica.staticFriction, Is.EqualTo(source.staticFriction));
                Assert.That(replica.bounciness, Is.EqualTo(source.bounciness));
                Assert.That(replica.frictionCombine, Is.EqualTo(source.frictionCombine));
                Assert.That(replica.bounceCombine, Is.EqualTo(source.bounceCombine));
                Assert.That(wire.Flags & 255, Is.EqualTo(2)); Assert.That(wire.Flags & (1u << 16), Is.Not.Zero);
                ulong signature = OnlineCollisionMaterialState.Signature(source); source.dynamicFriction *= .5f;
                Assert.That(OnlineCollisionMaterialState.Signature(source), Is.Not.EqualTo(signature));
                wire.Value3 = float.NaN; Assert.Throws<InvalidOperationException>(() => OnlineCollisionMaterialState.Create(wire));
                wire = packet; wire.Seed = 7; Assert.Throws<InvalidOperationException>(() => OnlineCollisionMaterialState.Create(wire));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(profile);
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (replica != null) UnityEngine.Object.DestroyImmediate(replica);
            }
        }

        [Test]
        public void BeveledCellChannelsAndInteriorMaterialSubmeshesSurviveTransfer()
        {
            var source = new Mesh(); Mesh replica = null;
            try
            {
                source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward };
                source.normals = new[] { Vector3.back, Vector3.right, Vector3.up, Vector3.forward };
                source.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(0, 1, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, -1) };
                source.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                source.uv2 = new[] { Vector2.one, Vector2.up, Vector2.right, Vector2.zero };
                source.colors32 = new[] { new Color32(30, 20, 10, 255), new Color32(60, 40, 20, 255), new Color32(90, 60, 30, 255), new Color32(120, 80, 40, 255) };
                source.subMeshCount = 2; source.SetTriangles(new[] { 0, 1, 2 }, 0); source.SetTriangles(new[] { 0, 3, 1 }, 1);
                byte[] wire = OnlineMeshCodec.Encode(source, out int length, out ulong hash);
                replica = OnlineMeshCodec.Decode(wire, length, hash);
                CollectionAssert.AreEqual(source.vertices, replica.vertices);
                CollectionAssert.AreEqual(source.normals, replica.normals);
                CollectionAssert.AreEqual(source.tangents, replica.tangents);
                CollectionAssert.AreEqual(source.uv, replica.uv); CollectionAssert.AreEqual(source.uv2, replica.uv2);
                CollectionAssert.AreEqual(source.colors32, replica.colors32);
                Assert.That(replica.subMeshCount, Is.EqualTo(2));
                CollectionAssert.AreEqual(source.GetIndices(0), replica.GetIndices(0));
                CollectionAssert.AreEqual(source.GetIndices(1), replica.GetIndices(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); if (replica != null) UnityEngine.Object.DestroyImmediate(replica); }
        }

        [Test]
        public void GeometryChecksumAndDeclaredLengthAreEnforced()
        {
            var source = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            try
            {
                byte[] wire = OnlineMeshCodec.Encode(source, out int length, out ulong hash);
                Assert.Throws<InvalidOperationException>(() => OnlineMeshCodec.Decode(wire, length, hash ^ 1));
                Assert.Throws<InvalidOperationException>(() => OnlineMeshCodec.Decode(wire, length + 1, hash));
                Assert.Throws<InvalidOperationException>(() => OnlineMeshCodec.Decode(wire, OnlineMeshCodec.MaximumRawBytes + 1, hash));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [Test]
        public void ShaderOverridesPreserveFractureMappingAndIndependentPiecePalette()
        {
            var source = new GameObject("Source"); var target = new GameObject("Replica");
            try
            {
                var original = source.AddComponent<MeshRenderer>(); var replica = target.AddComponent<MeshRenderer>();
                var props = new MaterialPropertyBlock();
                props.SetFloat("_MagicAmount", .25f); props.SetFloat("_FractureMappingEnabled", 1);
                props.SetColor("_ExteriorColor", new Color(.3f, .2f, .1f, 1));
                Matrix4x4 mapping = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), new Vector3(2, 1, .5f));
                props.SetMatrix("_FractureLocalToStructure", mapping); original.SetPropertyBlock(props);
                OnlinePacket wire = OnlineRendererState.Capture(original, props);
                Assert.That(OnlineRendererState.Apply(replica, wire, props), Is.True);
                replica.GetPropertyBlock(props);
                Assert.That(props.GetFloat("_MagicAmount"), Is.EqualTo(.25f));
                Assert.That(props.GetColor("_ExteriorColor"), Is.EqualTo(new Color(.3f, .2f, .1f, 1)));
                Assert.That(props.GetMatrix("_FractureLocalToStructure"), Is.EqualTo(mapping));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
