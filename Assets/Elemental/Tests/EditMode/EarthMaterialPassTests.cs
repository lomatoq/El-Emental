using System.Collections.Generic;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthMaterialPassTests
    {
        [Test]
        public void PerEventSettingsOverrideOnlyPresentationAndSurviveBatching()
        {
            var go = new GameObject("Per-event settings test");
            var profile = ScriptableObject.CreateInstance<EarthEffectsTuningProfile>();
            try
            {
                var rule = profile.MaterialEvents.For(EarthMaterialFeedbackKind.Extract);
                rule.dustCount = 17; rule.chipCount = 3; rule.particleSizeScale = 1.8f;
                var hub = go.AddComponent<EarthMaterialFeedbackHub>();
                hub.Configure(profile, null);
                EarthMaterialFeedbackCue received = default;
                hub.Presented += cue => received = cue;
                hub.Emit(EarthMaterialFeedbackKind.Extract, Vector3.zero, Vector3.up);
                hub.FlushPending();
                Assert.That(received.DustCount, Is.EqualTo(17));
                Assert.That(received.ChipCount, Is.EqualTo(3));
                Assert.That(received.ParticleSizeScale, Is.EqualTo(1.8f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); }
        }
        [Test]
        public void SeparatedHitsRetainContactLocationsAndParticleBudget()
        {
            var go = new GameObject("Material Feedback Test");
            try
            {
                var hub = go.AddComponent<EarthMaterialFeedbackHub>();
                var received = new List<EarthMaterialFeedbackCue>();
                hub.Presented += received.Add;
                for (uint i = 0; i < 8; i++) hub.Emit(EarthMaterialFeedbackKind.Fracture,
                    Vector3.right * (i * 5), Vector3.up, 1f, .5f, i + 1, dustCount: 140, chipCount: 28);
                hub.FlushPending();
                Assert.That(received.Count, Is.EqualTo(8));
                int dust = 0, chips = 0;
                for (int i = 0; i < received.Count; i++)
                {
                    Assert.That(received[i].Point.x, Is.EqualTo(i * 5));
                    Assert.That(received[i].DustCount, Is.GreaterThanOrEqualTo(8));
                    Assert.That(received[i].ChipCount, Is.GreaterThanOrEqualTo(2));
                    dust += received[i].DustCount; chips += received[i].ChipCount;
                }
                Assert.That(dust, Is.LessThanOrEqualTo(256)); Assert.That(chips, Is.LessThanOrEqualTo(64));
                Assert.That(hub.BudgetClampedParticles, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void NearbyEventsCoalesceAndOverflowIsObservable()
        {
            var go = new GameObject("Material Feedback Bounded Test");
            try
            {
                var hub = go.AddComponent<EarthMaterialFeedbackHub>();
                hub.Emit(EarthMaterialFeedbackKind.Impact, Vector3.zero, Vector3.up);
                hub.Emit(EarthMaterialFeedbackKind.Impact, Vector3.right, Vector3.up);
                Assert.That(hub.CoalescedEvents, Is.EqualTo(1));
                for (int i = 1; i < 12; i++) hub.Emit(EarthMaterialFeedbackKind.Impact, Vector3.right * i * 5, Vector3.up);
                Assert.That(hub.DroppedEvents, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
