from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=Path(__file__).parent/'after'
def write(p,s):
    q=out/p;q.parent.mkdir(parents=True,exist_ok=True);q.write_text(s,encoding='utf-8')
def edit(p,changes):
    s=(root/p).read_text(encoding='utf-8-sig')
    for a,b in changes:
        assert a in s,(p,a[:80]);s=s.replace(a,b,1)
    write(p,s)

edit('Assets/Elemental/Content/Shaders/LightDustMote.shader',[
('_Brightness("Brightness", Range(0, 4)) = 1.55','''_Brightness("Brightness", Range(0, 4)) = 1.55
        _FlipbookBlending("Particle Sheet Frame Blending", Range(0, 1)) = 0
        _FlipbookColumns("Particle Sheet Columns", Float) = 1
        _FlipbookRows("Particle Sheet Rows", Float) = 1'''),
('half _Brightness;','half _Brightness;\n                half _FlipbookBlending;\n                half _FlipbookColumns;\n                half _FlipbookRows;'),
('float2 uv : TEXCOORD0;','''// Shuriken UV + UV2 pack current/next frame coordinates in
                // TEXCOORD0; AnimBlend is TEXCOORD1.x. The sheet module owns
                // frame selection, never a global clock shared by all puffs.
                float4 uv : TEXCOORD0;
                float animBlend : TEXCOORD1;'''),
('float2 uv : TEXCOORD1;','float4 uv : TEXCOORD1;\n                half animBlend : TEXCOORD2;'),
('output.uv = input.uv;','output.uv = input.uv;\n                output.animBlend = input.animBlend;'),
('input.uv * 2.0 - 1.0','input.uv.xy * 2.0 - 1.0'),
('half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);','''half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                if (_FlipbookBlending > .5h)
                {
                    half4 nextSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw);
                    // Interpolate premultiplied frames: transparent black texels
                    // must not darken the silhouette during cross-fade.
                    half blend = saturate(input.animBlend);
                    half frameAlpha = lerp(baseSample.a, nextSample.a, blend);
                    half3 frameRgb = lerp(baseSample.rgb * baseSample.a,
                        nextSample.rgb * nextSample.a, blend);
                    baseSample = half4(frameRgb / max(frameAlpha, .0001h), frameAlpha);
                }''')])

edit('Assets/Elemental/Runtime/World/EarthEffectsTuningProfile.cs',[
('using System;','using System;\nusing System.Collections.Generic;'),
('    public static class EarthParticleSystemTuningApplier\n    {','''    public static class EarthParticleSystemTuningApplier
    {
        private static readonly int FlipbookBlendingId = Shader.PropertyToID("_FlipbookBlending");
        private static readonly int FlipbookColumnsId = Shader.PropertyToID("_FlipbookColumns");
        private static readonly int FlipbookRowsId = Shader.PropertyToID("_FlipbookRows");
        private static readonly List<ParticleSystemVertexStream> FlipbookStreams = new()
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend
        };

        public static void ConfigureDustFlipbook(ParticleSystem system)
        {
            if (system == null) return;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            // Existing ground wind/surf/motes remain on their original material
            // and UV contract. Only explicitly authored atlas materials opt in.
            if (material == null || !material.HasProperty(FlipbookBlendingId)) return;
            var sheet = system.textureSheetAnimation;
            if (material.GetFloat(FlipbookBlendingId) < .5f)
            {
                // A reused dust emitter must not keep atlas UVs after switching
                // back to the authored ground-wind/surf single sprite.
                sheet.enabled = false;
                return;
            }
            int columns = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(FlipbookColumnsId)));
            int rows = Mathf.Max(1, Mathf.RoundToInt(material.GetFloat(FlipbookRowsId)));
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = columns; sheet.numTilesY = rows;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            // With frame interpolation the terminal frame must not blend back
            // into frame zero. Nine authored images have eight transitions.
            float terminalFrame = (columns * rows - 1f) / (columns * rows);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(terminalFrame,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;
            sheet.uvChannelMask = UVChannelFlags.UV0;
            renderer.SetActiveVertexStreams(FlipbookStreams);
            renderer.receiveShadows = true;
        }
'''),
('            EarthEffectRenderOrder.ApplyDustRenderer(system.GetComponent<ParticleSystemRenderer>());','''            EarthEffectRenderOrder.ApplyDustRenderer(system.GetComponent<ParticleSystemRenderer>());
            ConfigureDustFlipbook(system);''')])

# A distinct material keeps the existing ground wind and surf texture unchanged.
newmat='Assets/Elemental/Content/GraphicsV5/Materials/WallcoeurEarthDust.mat'
mat=(root/'Assets/Elemental/Content/GraphicsV5/Materials/RumbleDustLit.mat').read_text(encoding='utf-8-sig')
mat=mat.replace('m_Name: RumbleDustLit','m_Name: WallcoeurEarthDust')
mat=mat.replace('b17e40cdf5d01df408ddfdc477469adf','296919e0817a9e944873a1f2c15b4a4c')
mat=mat.replace('- _FlipbookBlending: 0','- _FlipbookBlending: 1\n    - _FlipbookColumns: 3\n    - _FlipbookRows: 3')
write(newmat,mat)
guid='46021582908d47fa866f6f3f7c0a8e11'
write(newmat+'.meta',f'''fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
''')

profile='Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset'
s=(root/profile).read_text(encoding='utf-8-sig')
for field in ['fractureDust','impactDust']:
    s=s.replace(f'{field}: {{fileID: 2100000, guid: cc90a2dce6ef7d747955d2b1c251e5b2, type: 2}}',
        f'{field}: {{fileID: 2100000, guid: {guid}, type: 2}}')
# Large sheet silhouettes need fewer overlapping puffs, not hundreds of tiny cards.
start=s.index('  fracture:');end=s.index('  impact:');block=s[start:end]
for a,b in [('size: {x: 0.32, y: 1.25}','size: {x: 0.45, y: 1.45}'),
    ('baseCount: 105','baseCount: 48'),('perReleasedPiece: 34','perReleasedPiece: 12'),
    ('minimumCount: 180','minimumCount: 64'),('maximumCount: 340','maximumCount: 144')]:
    assert a in block,a;block=block.replace(a,b)
s=s[:start]+block+s[end:]
start=s.index('  impact:');end=s.index('  surf:');block=s[start:end]
for a,b in [('size: {x: 0.12, y: 0.46}','size: {x: 0.24, y: 0.7}'),
    ('maximumDustCount: 56','maximumDustCount: 24'),('maximumBatchedDustPerFrame: 85','maximumBatchedDustPerFrame: 40')]:
    assert a in block,a;block=block.replace(a,b,1)
s=s[:start]+block+s[end:];write(profile,s)

edit('Assets/Elemental/Authoring/Editor/EarthSurfaceWindDustSetup.cs',[
('effects.Materials.FractureDust == null','effects.Materials.SurfDust == null'),
('motor.GroundMask, effects.Materials.FractureDust','motor.GroundMask, effects.Materials.SurfDust')])

write('Assets/Elemental/Tests/EditMode/WallcoeurDustIntegrationTests.cs','''using System.Collections.Generic;
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
                Is.EqualTo("Assets/VFXPACK_FIRE_WALLCOEUR/Texture/A_Smoke_2.png"));
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
''')
