using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator SeismicVisionRevealsNightGeometryAndImmediatelyLosesAirborneSupport()
        {
            Actor actor = _actors.Find(a => a.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            MagicInputController input = motor.GetComponent<MagicInputController>();
            EarthSeismicVision vision = motor.GetComponent<EarthSeismicVision>();
            Assert.That(input, Is.Not.Null);
            Assert.That(vision, Is.Not.Null);
            CelestialSystemBehaviour celestial = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<CelestialSystemBehaviour>();
                if (found != null) celestial = found;
            }
            Assert.That(celestial, Is.Not.Null);
            float oldPhase = celestial.Snapshot.TimeOfDay01;
            bool oldRequested = vision.Requested;
            try
            {
                Assert.That(motor.HasStableSupport, Is.True);
                celestial.SetTimeOfDayForQa(.75f);
                vision.SetActive(true);
                yield return new WaitForSeconds(.5f);
                yield return _frame;
                Assert.That(vision.IsActive, Is.True);
                Assert.That(vision.VisiblePulseCount, Is.GreaterThan(0));
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.EqualTo(1f));
                string folder = Path.GetFullPath("BuildReports/EnvironmentAnimationRescue/SeismicVision");
                Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightGrounded.png"));
                yield return _frame;
                motor.BeginExternalLaunch(12);
                motor.Body.linearVelocity += motor.LocalUp * 5f;
                yield return _frame;
                Assert.That(vision.IsActive, Is.False, "Airborne perception must stop on the first rendered frame.");
                Assert.That(vision.VisiblePulseCount, Is.Zero, "Old waves survived loss of support.");
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.Zero);
                Assert.That(vision.Requested, Is.True, "Jumping suspends the ability without erasing the player's toggle.");
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "NightAirborne.png"));
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!vision.IsActive && Time.realtimeSinceStartupAsDouble < deadline) yield return _frame;
                Assert.That(vision.IsActive, Is.True, "Ground contact must resume fresh waves.");
                vision.SetActive(false);
                yield return _frame;
                Assert.That(vision.IsActive, Is.False);
                Assert.That(Shader.GetGlobalFloat("_EarthSeismicVision"), Is.Zero);
            }
            finally
            {
                vision.SetActive(oldRequested);
                celestial.SetTimeOfDayForQa(oldPhase);
            }
        }
    }
}
