using Elemental.Authoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthParticleMaterialValidatorTests
    {
        [Test]
        public void RumbleDustMaterialIsTransparentTexturedAndSoft()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Elemental/Content/GraphicsV5/Materials/RumbleDustLit.mat");
            Assert.That(material, Is.Not.Null);
            var go = new GameObject("Chunky Earth Dust");
            try
            {
                go.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
                Assert.That(EarthParticleMaterialValidator.ValidateRenderer(renderer, out string error),
                    Is.True,
                    error);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
