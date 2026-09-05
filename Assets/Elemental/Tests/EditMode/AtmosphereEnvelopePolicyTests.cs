using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class AtmosphereEnvelopePolicyTests
    {
        [Test]
        public void EffectiveEnvelopeUsesFixedPhysicalHeightAndSanitizesLegacyValues()
        {
            const float radius = 55.1f;
            Assert.That(
                AtmosphereEnvelopePolicy.EffectiveOuterRadius(radius, 1.055f, 8f),
                Is.EqualTo(63.1f).Within(.00001f));
            Assert.That(
                AtmosphereEnvelopePolicy.EffectiveOuterRadius(radius, 1.2f, 8f),
                Is.EqualTo(radius * 1.2f).Within(.00001f));
            Assert.That(
                AtmosphereEnvelopePolicy.EffectiveOuterRadius(radius, 1.055f, 0f),
                Is.EqualTo(63.1f).Within(.00001f),
                "A missing serialized minimum-height field must use the safe legacy default.");
            Assert.That(
                AtmosphereEnvelopePolicy.EffectiveOuterRadius(radius, float.NaN, float.NaN),
                Is.EqualTo(63.1f).Within(.00001f));
        }

        [Test]
        public void SystemPlanetRemainsHiddenThroughEnvelopeThenRevealsMonotonically()
        {
            const float radius = 55.1f;
            const float outer = 63.1f;
            const float reveal = .96f;
            Assert.That(AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, 61.035f), Is.Zero);
            Assert.That(AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, outer), Is.Zero);
            Assert.That(
                AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, outer + reveal * .5f),
                Is.EqualTo(.5f).Within(.00001f));
            Assert.That(
                AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, outer + reveal),
                Is.EqualTo(1f).Within(.00001f));
            Assert.That(AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, float.NaN), Is.Zero);

            float previous = 0f;
            for (int index = 0; index <= 64; index++)
            {
                float observer = outer + reveal * index / 64f;
                float current = AtmosphereEnvelopePolicy.SystemBodyVisibility(radius, outer, observer);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                previous = current;
            }
        }

        [Test]
        public void ProfileAndPurePolicyAgreeForCurrentAndLegacySerializedData()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("outerRadiusMultiplier").floatValue = 1.055f;
                serialized.FindProperty("minimumAtmosphereHeightMeters").floatValue = 8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.EffectiveOuterRadius(55.1f), Is.EqualTo(63.1f).Within(.00001f));
                Assert.That(profile.SystemBodyVisibility(55.1f, 61.035f), Is.Zero);
                Assert.That(profile.SystemBodyVisibility(55.1f, 63.58f), Is.EqualTo(.5f).Within(.0001f));

                serialized.FindProperty("minimumAtmosphereHeightMeters").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.MinimumAtmosphereHeightMeters,
                    Is.EqualTo(AtmosphereEnvelopePolicy.DefaultMinimumHeightMeters));
                Assert.That(profile.EffectiveOuterRadius(55.1f), Is.EqualTo(63.1f).Within(.00001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
