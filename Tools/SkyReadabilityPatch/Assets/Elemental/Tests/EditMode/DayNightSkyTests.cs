using System.Collections.Generic;
using Elemental.Simulation.Time;
using NUnit.Framework;
using Unity.Mathematics;
using Elemental.Authoring.Editor;
using Elemental.Runtime.World;
using UnityEngine;
using UnityEditor;

namespace Elemental.Tests.EditMode
{
    public sealed class DayNightSkyTests
    {
        [Test]
        public void SunriseToSunsetIsFiveMinutesAndNightHasItsOwnDuration()
        {
            Assert.That(CelestialDayNightCycle.Phase(0, 0, 300, 120), Is.EqualTo(0));
            Assert.That(CelestialDayNightCycle.Phase(150, 0, 300, 120), Is.EqualTo(.25f));
            Assert.That(CelestialDayNightCycle.Phase(300, 0, 300, 120), Is.EqualTo(.5f));
            Assert.That(CelestialDayNightCycle.Phase(360, 0, 300, 120), Is.EqualTo(.75f));
            Assert.That(CelestialDayNightCycle.Phase(420, 0, 300, 120), Is.EqualTo(0));
        }

        [Test]
        public void PhaseSeekWrapPauseAndUnequalDurationsRemainConsistent()
        {
            foreach (float phase in new[] { 0f, .21f, .499f, .5f, .73f, .999f })
            {
                double seek = CelestialDayNightCycle.SecondsAtPhase(phase, 300, 170) -
                    CelestialDayNightCycle.SecondsAtPhase(.21f, 300, 170);
                Assert.That(CelestialDayNightCycle.Phase(seek, .21f, 300, 170), Is.EqualTo(phase).Within(.000001));
            }
            Assert.That(CelestialDayNightCycle.Phase(-60, 0, 300, 120), Is.EqualTo(.75f));
            Assert.That(CelestialDayNightCycle.Phase(double.NaN, 0, float.NaN, float.PositiveInfinity), Is.EqualTo(0));
            Assert.That(CelestialLightingClockPolicy.Step(50, .02f, 0, CelestialLightingAuthorityMode.AnimatedEphemeris), Is.EqualTo(50));
        }

        [Test]
        public void DaylightAndNightUseObserverRadialUpAndSolarLightStopsBelowHorizon()
        {
            var sun = new float3(1, 0, 0);
            Assert.That(CelestialDayNightCycle.Night(sun, sun), Is.EqualTo(0));
            Assert.That(CelestialDayNightCycle.Night(sun, -sun), Is.EqualTo(1));
            Assert.That(CelestialDayNightCycle.SolarStrength(-.1f), Is.Zero);
            Assert.That(CelestialDayNightCycle.SolarStrength(.8f), Is.EqualTo(1));
        }

        [Test]
        public void StarsAreDeterministicNormalizedAndDistributedByEqualAreaWithoutRows()
        {
            const uint count = 12000, seed = 57824;
            var bins = new int[12];
            var latitudes = new HashSet<float>();
            float3 mean = float3.zero;
            for (uint i = 0; i < count; i++)
            {
                float3 direction = CelestialStarDistribution.Direction(i, seed);
                Assert.That(math.length(direction), Is.EqualTo(1).Within(.000001));
                Assert.That(math.all(direction == CelestialStarDistribution.Direction(i, seed)), Is.True);
                bins[math.min(11, (int)((direction.y + 1f) * 6f))]++;
                latitudes.Add(direction.y);
                mean += direction;
            }
            foreach (int countInBand in bins) Assert.That(countInBand, Is.InRange(840, 1160));
            Assert.That(latitudes.Count, Is.GreaterThan(11970), "No fixed rings or repeated latitude rows.");
            Assert.That(math.length(mean / count), Is.LessThan(.025f));
        }

        [Test]
        public void LegacyFullCycleIsMigratedOnceAndNewDefaultsStayFiveMinutesEach()
        {
            var defaults = ScriptableObject.CreateInstance<CelestialSystemProfile>();
            string path = "Assets/Elemental/Tests/LegacyCelestialMigration_" + System.Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                Assert.That(defaults.DaylightSeconds, Is.EqualTo(300));
                Assert.That(defaults.NightSeconds, Is.EqualTo(300));
                Assert.That(defaults.NightAmbientIntensity, Is.EqualTo(1.05f));
                Assert.That(defaults.MoonlightIntensity, Is.EqualTo(.80f));
                string guid = AssetDatabase.AssetPathToGUID("Assets/Elemental/Runtime/World/CelestialSystemProfile.cs");
                Assert.That(guid, Is.Not.Empty);
                // Exercise actual asset migration, including FormerlySerializedAs,
                // rather than assuming EditorJsonUtility's native object JSON layout.
                string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                    "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                    "  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}\n  m_Name: LegacyCelestialMigration\n" +
                    "  m_EditorClassIdentifier: Elemental.Runtime::Elemental.Runtime.World.CelestialSystemProfile\n  daySeconds: 480\n";
                System.IO.File.WriteAllText(path, yaml.Replace("\\n", "\n"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var legacy = AssetDatabase.LoadAssetAtPath<CelestialSystemProfile>(path);
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.DaylightSeconds, Is.EqualTo(240));
                Assert.That(legacy.NightSeconds, Is.EqualTo(240));
                var serialized = new SerializedObject(legacy);
                serialized.FindProperty("daylightSeconds").floatValue = 300;
                serialized.FindProperty("nightSeconds").floatValue = 120;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(legacy);
                AssetDatabase.SaveAssetIfDirty(legacy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<CelestialSystemProfile>(path);
                Assert.That(reloaded.DaySeconds, Is.EqualTo(420), "Migration must not overwrite subsequent authored durations.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                Object.DestroyImmediate(defaults);
            }
        }

        [Test]
        public void MissingLegacyNightExposureFieldsUseReadableConservativeDefaults()
        {
            var celestial = ScriptableObject.CreateInstance<CelestialSystemProfile>();
            var sky = ScriptableObject.CreateInstance<Elemental.Presentation.Rendering.EarthSkyProfile>();
            try
            {
                var celestialSerialized = new SerializedObject(celestial);
                celestialSerialized.FindProperty("nightAmbientIntensity").floatValue = 0f;
                celestialSerialized.ApplyModifiedPropertiesWithoutUndo();
                var skySerialized = new SerializedObject(sky);
                skySerialized.FindProperty("nightSkyIntensity").floatValue = 0f;
                skySerialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(celestial.NightAmbientIntensity, Is.EqualTo(1.05f));
                Assert.That(sky.NightSkyIntensity, Is.EqualTo(1.45f));
            }
            finally
            {
                Object.DestroyImmediate(celestial);
                Object.DestroyImmediate(sky);
            }
        }

        [Test]
        public void ElevatedObserverSeesSunBelowTangentUntilActualPlanetHorizon()
        {
            var observer = new float3(0, 58, 0);
            Assert.That(CelestialDayNightCycle.PlanetOccludesRay(observer, math.normalize(new float3(1, -.15f, 0)), 55), Is.False);
            Assert.That(CelestialDayNightCycle.PlanetOccludesRay(observer, math.normalize(new float3(1, -.5f, 0)), 55), Is.True);
        }

        [Test]
        public void CubeFaceBasesShareAllEdgesAndCornersInWorldDirection()
        {
            // Protect actual cubemap face orientation: each interior edge belongs to two
            // faces and each corner to three, all describing the same world-space ray.
            for (int face = 0; face < 6; face++)
            {
                DayNightSkyRestore.FaceBasis(face, out Vector3 f, out Vector3 r, out Vector3 u);
                foreach (Vector2 uv in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1),
                    new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
                {
                    Vector3 direction = (f + r * uv.x + u * uv.y).normalized;
                    int faces = 0;
                    for (int other = 0; other < 6; other++)
                    {
                        DayNightSkyRestore.FaceBasis(other, out Vector3 of, out Vector3 otherRight, out Vector3 ou);
                        float depth = Vector3.Dot(direction, of);
                        if (depth <= 0) continue;
                        float x = Vector3.Dot(direction, otherRight) / depth, y = Vector3.Dot(direction, ou) / depth;
                        if (Mathf.Abs(x) > 1.00001f || Mathf.Abs(y) > 1.00001f) continue;
                        Assert.That(Vector3.Distance(direction, (of + otherRight * x + ou * y).normalized), Is.LessThan(.00001f));
                        faces++;
                    }
                    Assert.That(faces, Is.EqualTo(Mathf.Abs(uv.x) + Mathf.Abs(uv.y) > 1 ? 3 : 2));
                }
            }
        }
    }
}
