using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ShippingGroundWaveCommitRendersContactWithoutReplayingWindup()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            EarthPillarWaveAbility wave = actor.Presentation.GetComponentInParent<EarthPillarWaveAbility>();
            PlanetMotor motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            Assert.That(wave, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            pose.CancelPresentationForAnimationOwnership();
            yield return _frame;

            Assert.That(wave.TryCast(
                motor.FacingForward,
                .32f,
                .55f,
                out EarthTechniqueRejectReason rejection),
                Is.True,
                rejection.ToString());
            uint sequence = pose.LastAuthoritativeTick;
            Assert.That(pose.CurrentRequest.Technique, Is.EqualTo(EarthTechniqueId.WebWave));
            Assert.That(pose.AuthoritativeStartsAtContact, Is.True,
                "The successfully launched wave did not publish a committed contact boundary.");

            float simulatedSeconds = 0f;
            int frames = 0;
            double watchdog = Time.realtimeSinceStartupAsDouble + 1d;
            while ((!pose.RenderedContactReached || pose.LastAuthoritativeTick != sequence) &&
                   Time.realtimeSinceStartupAsDouble < watchdog)
            {
                yield return _frame;
                simulatedSeconds += Mathf.Clamp(Time.deltaTime, 0f, .1f);
                frames++;
            }

            Debug.Log(
                $"[GroundWaveCommit] sequence={sequence} frames={frames} " +
                $"simulated={simulatedSeconds:F4} clock={actor.Presentation.MagicClipTime:F4} " +
                $"columns={wave.LastColumnCount}");
            Assert.That(pose.RenderedContactReached, Is.True,
                "The world wave launched but its authored contact was never rendered.");
            Assert.That(simulatedSeconds, Is.LessThanOrEqualTo(.25f),
                "The wave replayed its long wind-up after columns had already launched.");
            Assert.That(frames, Is.LessThanOrEqualTo(16));
            pose.CancelPresentationForAnimationOwnership();
        }
    }
}
