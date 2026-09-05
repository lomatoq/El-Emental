using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    /// <summary>Actual GPU pixels for the candidate temporal helper and inactive branch.</summary>
    public sealed class SeismicVisionTemporalPixelTests
    {
        private const int StripHeight = 16;
        private const float RadiusSpeed = 10f;
        private const float FixedWorldDistance = 10f;
        private const float StartRadius = 9.56f;
        private const float EndRadius = 10.72f;
        private static readonly int TestModeId = Shader.PropertyToID("_TestMode");
        private static readonly int TestRadialDistanceId = Shader.PropertyToID("_TestRadialDistance");
        private static readonly int TestCurrentRadiusId = Shader.PropertyToID("_TestCurrentRadius");
        private static readonly int TestRadiusTravelId = Shader.PropertyToID("_TestRadiusTravel");
        private static readonly int TestWidthId = Shader.PropertyToID("_TestWidth");
        private static readonly int ActiveId = Shader.PropertyToID("_EarthSeismicVision");
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Hidden/Elemental/Tests/Seismic Temporal Pixel");
            Assert.That(shader, Is.Not.Null, "Temporal pixel test shader was not imported.");
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);
            if (_material != null) UnityEngine.Object.DestroyImmediate(_material);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void MovingFrontHasMeasuredIntermediateGpuSampleAndWritesTemporalStrip(int hz)
        {
            float step = RadiusSpeed / hz;
            var samples = new List<byte>();
            var currentOnlySamples = new List<byte>();
            float previous = StartRadius;
            for (float current = StartRadius; current <= EndRadius + 0.0001f; current += step)
            {
                float travel = Mathf.Max(0f, current - previous);
                samples.Add(RenderPulse(current, travel, true));
                currentOnlySamples.Add(RenderPulse(current, travel, false));
                previous = current;
            }

            int firstPeak = samples.FindIndex(value => value >= 190);
            Assert.That(firstPeak, Is.GreaterThan(0), $"{hz} Hz sequence never reached the pulse peak.");
            bool hasIntermediate = false;
            for (int i = 0; i < firstPeak; i++)
                hasIntermediate |= samples[i] > 15 && samples[i] < 190;
            Assert.That(hasIntermediate, Is.True,
                $"{hz} Hz front jumped from baseline to peak without a temporal coverage sample: {string.Join(",", samples)}");

            if (hz == 30)
            {
                Assert.That(currentOnlySamples[0], Is.LessThanOrEqualTo(2));
                Assert.That(currentOnlySamples[1], Is.GreaterThanOrEqualTo(245),
                    "The deterministic 30 Hz fixture no longer reproduces the unfiltered dark-to-peak jump.");
                Assert.That(samples[1], Is.InRange(105, 150),
                    "The candidate did not turn the reproduced jump into a half-coverage frame.");
            }

            string folder = Path.GetFullPath("BuildReports/SeismicVisionTemporal");
            Directory.CreateDirectory(folder);
            WriteStrip(samples, Path.Combine(folder, $"Temporal-{hz}Hz.png"));
            WriteStrip(currentOnlySamples, Path.Combine(folder, $"CurrentOnly-{hz}Hz.png"));
        }

        [TestCase(0.72f, 0.82f, 0.95f, "Day")]
        [TestCase(0.025f, 0.035f, 0.06f, "Night")]
        public void InactiveBranchIsByteExactForDeterministicDayAndNightSource(
            float red, float green, float blue, string label)
        {
            var source = new Texture2D(32, 16, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                for (int y = 0; y < source.height; y++)
                    for (int x = 0; x < source.width; x++)
                    {
                        float modulation = (x + y * source.width) % 7 / 255f;
                        source.SetPixel(x, y, new Color(red + modulation, green, blue, 1f));
                    }
                source.Apply(false, false);
                Shader.SetGlobalFloat(ActiveId, 0f);
                Color32[] direct = Render(source, 0f, source.width, source.height);
                Color32[] inactive = Render(source, 1f, source.width, source.height);
                Assert.That(inactive, Is.EqualTo(direct),
                    $"Inactive seismic path changed deterministic {label} pixels.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private byte RenderPulse(float currentRadius, float radiusTravel, bool temporal)
        {
            _material.SetFloat(TestModeId, temporal ? 2f : 3f);
            _material.SetFloat(TestRadialDistanceId, FixedWorldDistance);
            _material.SetFloat(TestCurrentRadiusId, currentRadius);
            _material.SetFloat(TestRadiusTravelId, radiusTravel);
            _material.SetFloat(TestWidthId, 0.12f);
            Color32[] pixel = Render(Texture2D.whiteTexture, temporal ? 2f : 3f, 1, 1);
            return pixel[0].r;
        }

        private static void WriteStrip(IReadOnlyList<byte> samples, string path)
        {
            var strip = new Texture2D(samples.Count, StripHeight, TextureFormat.RGB24, false, true);
            try
            {
                for (int x = 0; x < samples.Count; x++)
                    for (int y = 0; y < StripHeight; y++)
                    {
                        byte value = samples[x];
                        strip.SetPixel(x, y, new Color32(value, value, value, 255));
                    }
                strip.Apply(false, false);
                File.WriteAllBytes(path, strip.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strip);
            }
        }

        private Color32[] Render(Texture source, float mode, int width, int height)
        {
            _material.SetFloat(TestModeId, mode);
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear) { antiAliasing = 1 };
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                Graphics.Blit(source, target, _material, 0);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }
    }
}
