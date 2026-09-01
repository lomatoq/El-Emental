using System;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class RenderingGateInvariantTests
    {
        [Test]
        public void EarthCoreLegacyShadowPolicyDisablesAtlasCascadesAndDistance()
        {
            EarthArenaShadowQualitySettings settings =
                EarthArenaShadowQualityPolicy.Resolve(true);

            Assert.That(settings.ShadowQuality, Is.EqualTo(ShadowQuality.Disable));
            Assert.That(settings.CascadeCount, Is.Zero);
            Assert.That(settings.ShadowDistance, Is.Zero);
        }

        [Test]
        public void NonArenaLegacyShadowPolicyRetainsExplicitCompatibilityValues()
        {
            EarthArenaShadowQualitySettings settings =
                EarthArenaShadowQualityPolicy.Resolve(false);

            Assert.That(settings.ShadowQuality, Is.EqualTo(ShadowQuality.All));
            Assert.That(settings.CascadeCount, Is.EqualTo(4));
            Assert.That(settings.ShadowDistance, Is.EqualTo(90f));
            Assert.That(settings.ShadowResolution, Is.EqualTo(ShadowResolution.High));
        }

        [Test]
        public void MovingDualSubjectEvidencePathAllocatesNothingAfterWarmup()
        {
            var cameraObject = new GameObject("Gate Camera");
            var primary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var secondary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cameraObject.AddComponent<UnityEngine.Camera>();
                EarthCinematicDepthOfFieldController controller =
                    cameraObject.AddComponent<EarthCinematicDepthOfFieldController>();
                primary.transform.SetPositionAndRotation(
                    new Vector3(-1.5f, 0f, 8f), Quaternion.identity);
                secondary.transform.SetPositionAndRotation(
                    new Vector3(2f, 0f, 13f), Quaternion.identity);
                controller.ConfigureSubjects(primary.transform, secondary.transform);

                for (int index = 0; index < 16; index++)
                {
                    cameraObject.transform.position = new Vector3(index * 0.001f, 0f, 0f);
                    controller.TryGetSharpEnvelopeEvidence(out _);
                }

                long before = GC.GetAllocatedBytesForCurrentThread();
                bool finite = true;
                for (int index = 0; index < 128; index++)
                {
                    cameraObject.transform.position = new Vector3(index * 0.001f, 0f, 0f);
                    finite &= controller.TryGetSharpEnvelopeEvidence(out _);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(finite, Is.True);
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primary);
                UnityEngine.Object.DestroyImmediate(secondary);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
