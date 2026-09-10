using Elemental.Runtime.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class HardPolishSavedRockGeometryTests
    {
        [Test]
        public void SavedStoneMaterialsUseGeometryNormalsAndMatteResponse()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Elemental/Content/GraphicsV5/Materials" });
            int checkedMaterials = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_StoneMatte") || material.GetFloat("_SurfaceMode") > .5f) continue;
                Assert.That(material.GetFloat("_SideShadingSmoothness"), Is.Zero, path + " must shade actual mesh normals rather than radial cylinder normals.");
                Assert.That(material.GetFloat("_StoneMatte"), Is.EqualTo(1), path + " must not introduce a view-dependent glossy highlight.");
                checkedMaterials++;
            }
            Assert.That(checkedMaterials, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void ActualSavedRenderAndPhysicsMeshesRemainClosedUnderAnisotropicScale()
        {
            const string root = "Assets/Elemental/Content/GraphicsV5/";
            string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { root + "Rocks", root + "Physics" });
            int renderCount = 0, physicsCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool physics = path.Contains("/Physics/");
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.That(mesh, Is.Not.Null, path);
                var policy = new EarthMeshIntegrityPolicy(true, true, false, physics ? 255 : 4096,
                    weldTolerance: 0.000001f, strictFlatNormals: true);
                foreach (Vector3 scale in new[] { Vector3.one, new Vector3(0.55f, 1.7f, 0.8f) })
                {
                    var report = EarthMeshIntegrityValidator.Validate(mesh, policy,
                        Matrix4x4.TRS(new Vector3(7, -2, 11), Quaternion.Euler(13, 41, 27), scale));
                    Assert.That(report.IsValid, Is.True, $"{path}, scale={scale}: {report}");
                    Assert.That(report.ComponentCount, Is.EqualTo(1), path);
                    Assert.That(report.SignedVolume, Is.GreaterThan(0), path);
                }
                if (physics) physicsCount++; else renderCount++;
            }
            Assert.That(renderCount, Is.EqualTo(20));
            Assert.That(physicsCount, Is.EqualTo(12));
        }
    }
}
