using System;
using System.Collections;
using System.IO;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPowerVfxRuntimeTests
    {
        [UnityTest] public IEnumerator ProductionExtractionHasManyChipsAndSmokeWithinSharedBudget()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production-asset integration test requires Editor Play Mode.");
            yield break;
#else
            var profile = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.MaterialEvents.For(EarthMaterialFeedbackKind.Extract).chipCount, Is.GreaterThanOrEqualTo(40), "Apply Capture Dust and Sunlight before running this acceptance.");
            GameObject root = new GameObject("Capture particle integration proof");
            root.SetActive(false);
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shape.SetActive(false);
            try
            {
                var hub = root.AddComponent<EarthMaterialFeedbackHub>();
                hub.Configure(profile, root.transform);
                var presenter = root.AddComponent<EarthMaterialFeedbackPresenter>();
                var dust = MakeParticles(root, "Impact dust");
                var chips = MakeParticles(root, "Stone chips");
                var broad = MakeParticles(root, "Broad extraction smoke");
                presenter.Configure(hub, profile, null, dust, chips, shape.GetComponent<MeshFilter>().sharedMesh, broad);
                root.SetActive(true);
                yield return null;
                hub.Emit(EarthMaterialFeedbackKind.Extract, Vector3.up * 55.1f, Vector3.up, 1f, 1f);
                hub.FlushPending();
                Assert.That(chips.particleCount, Is.GreaterThanOrEqualTo(40));
                Assert.That(broad.particleCount, Is.GreaterThanOrEqualTo(90));
                int initialChips = chips.particleCount, initialSmoke = broad.particleCount;
                for (int i = 0; i < 80; i++)
                    hub.Emit(EarthMaterialFeedbackKind.Fracture, Vector3.up * 55.1f + Vector3.right * i * 2f, Vector3.up, 3f, 1f, (uint)i);
                int beforeChips = chips.particleCount, beforeDust = broad.particleCount;
                hub.FlushPending();
                Assert.That(chips.particleCount - beforeChips, Is.LessThanOrEqualTo(profile.MaterialEvents.chipsPerFrame));
                Assert.That(broad.particleCount - beforeDust, Is.LessThanOrEqualTo(profile.MaterialEvents.dustPerFrame));
                Assert.That(hub.BudgetClampedParticles, Is.GreaterThan(0));
                Assert.That(chips.main.maxParticles, Is.LessThanOrEqualTo(768));
                Assert.That(broad.main.maxParticles, Is.LessThanOrEqualTo(2000));
                yield return new WaitForSeconds(2.3f);
                Assert.That(chips.particleCount, Is.Zero, "Cosmetic chips must retire without physical bodies.");
                Assert.That(broad.particleCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Rigidbody>().Length, Is.Zero);
                Directory.CreateDirectory("BuildReports/PowerVfx");
                File.WriteAllText("BuildReports/PowerVfx/particles.json", "{\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"initialChips\":" + initialChips + ",\"initialSmoke\":" + initialSmoke + ",\"budgetClamped\":" + hub.BudgetClampedParticles + ",\"retired\":true}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(shape); }
#endif
        }
        private static ParticleSystem MakeParticles(GameObject parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent.transform, false);
            return child.AddComponent<ParticleSystem>();
        }
    }
}
