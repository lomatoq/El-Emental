using System.Collections;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator ConfirmedGameplayBoundariesRenderContactPromptlyForAllSemanticSlots()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            EarthCharacterPoseController pose = actor.Presentation.PoseController;
            FieldInfo profileField = typeof(HumanoidCharacterPresentation).GetField(
                "magicMotionProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            EarthMagicMotionProfile profile =
                profileField?.GetValue(actor.Presentation) as EarthMagicMotionProfile;
            Assert.That(profile, Is.Not.Null);
            EarthTechniqueId[] techniques =
            {
                EarthTechniqueId.RaiseWall, EarthTechniqueId.RaisePlatform,
                EarthTechniqueId.PullStone, EarthTechniqueId.ThrowStone,
                EarthTechniqueId.VectorPush, EarthTechniqueId.Repair,
                EarthTechniqueId.Resonance, EarthTechniqueId.PillarJump,
                EarthTechniqueId.Armor, EarthTechniqueId.ArmorBarrage,
                EarthTechniqueId.MeteorFinish
            };

            for (int index = 0; index < techniques.Length; index++)
            {
                EarthTechniqueId technique = techniques[index];
                int slot = (int)EarthHumanoidMotionResolver.Resolve(technique);
                EarthMagicMotionEntry motion = profile.Find(slot);
                Assert.That(motion, Is.Not.Null, technique.ToString());
                pose.CancelPresentationForAnimationOwnership();
                yield return _frame;

                uint sequence = 0xfd000000u + (uint)index;
                pose.RequestSemanticPresentation(
                    MagicPresentationSemanticResolver.ResolveKind(technique),
                    technique,
                    sequence,
                    actor.Presentation.transform.position +
                    actor.Presentation.transform.forward * 3f,
                    80f,
                    8f,
                    immediateActionBoundary: true);

                float simulatedSeconds = 0f;
                int renderedFrames = 0;
                double watchdog = Time.realtimeSinceStartupAsDouble + 1d;
                while ((!pose.RenderedContactReached ||
                        pose.LastAuthoritativeTick != sequence) &&
                       Time.realtimeSinceStartupAsDouble < watchdog)
                {
                    yield return _frame;
                    simulatedSeconds += Mathf.Clamp(Time.deltaTime, 0f, .1f);
                    renderedFrames++;
                }

                Debug.Log(
                    $"[CommittedMagicContact] technique={technique} slot={slot} " +
                    $"frames={renderedFrames} simulated={simulatedSeconds:F4} " +
                    $"clock={actor.Presentation.MagicClipTime:F4} " +
                    $"contact={motion.timing.Contact:F4}");
                Assert.That(pose.LastAuthoritativeTick, Is.EqualTo(sequence));
                Assert.That(pose.RenderedContactReached, Is.True,
                    $"{technique} never contributed its confirmed contact pose.");
                Assert.That(actor.Presentation.MagicClipTime,
                    Is.GreaterThanOrEqualTo(motion.timing.Contact - .001f),
                    $"{technique} reported contact before its clip reached the marker.");
                Assert.That(simulatedSeconds, Is.LessThanOrEqualTo(.25f),
                    $"{technique} replayed wind-up after its gameplay result was committed.");
                Assert.That(renderedFrames, Is.LessThanOrEqualTo(16),
                    $"{technique} contact admission stalled across too many rendered evaluations.");
            }

            pose.CancelPresentationForAnimationOwnership();
        }
    }
}
