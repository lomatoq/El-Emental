using System.Collections.Generic;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Tests.EditMode
{
    public sealed class WallcoeurDustIntegrationTests
    {
        [Test] public void BurstDustUsesRealNineFrameAtlasButGroundWindKeepsOriginalMaterial()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>(
                "Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            Assert.That(profile.Materials.FractureDust, Is.SameAs(profile.Materials.ImpactDust));
            Assert.That(profile.Materials.SurfDust, Is.Not.SameAs(profile.Materials.ImpactDust));
            Assert.That(AssetDatabase.GetAssetPath(profile.Materials.ImpactDust.GetTexture("_BaseMap")),
                Is.EqualTo("Assets/VFXPACK_FIRE_WALLCOEUR/Texture/a_VFX_flame.png"));
            var host = new GameObject("Wallcoeur dust sheet contract");
            try
            {
                var ps = host.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                EarthParticleSystemTuningApplier.ApplyDust(ps, profile.Impact.Dust, profile.Materials.ImpactDust);
                var sheet = ps.textureSheetAnimation;
                Assert.That(sheet.enabled, Is.True);
                Assert.That(sheet.numTilesX, Is.EqualTo(3)); Assert.That(sheet.numTilesY, Is.EqualTo(3));
                Assert.That(sheet.timeMode, Is.EqualTo(ParticleSystemAnimationTimeMode.Lifetime));
                Assert.That(sheet.startFrame.constant, Is.Zero); Assert.That(sheet.cycleCount, Is.EqualTo(1));
                Assert.That(sheet.frameOverTime.Evaluate(0f), Is.Zero.Within(.001f));
                Assert.That(sheet.frameOverTime.Evaluate(1f), Is.EqualTo(8f / 9f).Within(.001f));
                Assert.That(sheet.frameOverTime.Evaluate(.85f), Is.GreaterThanOrEqualTo(8f / 9f-.001f),
                    "The rolling silhouette must finish before the alpha tail, without looping.");
                var streams = new List<ParticleSystemVertexStream>();
                ps.GetComponent<ParticleSystemRenderer>().GetActiveVertexStreams(streams);
                Assert.That(streams, Does.Contain(ParticleSystemVertexStream.UV2));
                Assert.That(streams, Does.Contain(ParticleSystemVertexStream.AnimBlend));
                EarthParticleSystemTuningApplier.ApplyDust(ps, profile.Impact.Dust, profile.Materials.SurfDust);
                Assert.That(ps.textureSheetAnimation.enabled, Is.False,
                    "The old wind/surf sprite must not retain atlas UVs on a reused emitter.");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
