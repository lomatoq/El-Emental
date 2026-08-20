using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEditor;
using Elemental.Input.Gestures;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthActionIntentResolverTests
    {
        [Test]
        public void ExplicitPriorityChoosesExactlyOneOwner()
        {
            var everything = new EarthGestureFrame(
                cancelPressed: true,
                grounded: true,
                descending: true,
                moveMagnitude: 1f,
                modifierHeld: true,
                jumpPressed: true,
                landingWaveArmed: true,
                primaryHeld: true,
                primaryHeldSeconds: 1f,
                forceHeld: true,
                fieldHeld: true,
                hasControlledTarget: true,
                hasPrimedQuickStone: true,
                hasRepairTarget: true);

            EarthActionIntent result = EarthActionIntentResolver.Resolve(in everything);

            Assert.That(result.Kind, Is.EqualTo(EarthActionIntentKind.Cancel));
            Assert.That(result.Consumption, Is.EqualTo(EarthInputConsumption.Cancel));
        }

        [TestCase(false, true, true, 0f, EarthActionIntentKind.LandingWave)]
        [TestCase(true, false, true, 0.8f, EarthActionIntentKind.Surf)]
        [TestCase(true, false, true, 0f, EarthActionIntentKind.SelfRadialWave)]
        public void MobilityPriorityIsDeterministic(
            bool grounded,
            bool descending,
            bool modifier,
            float move,
            EarthActionIntentKind expected)
        {
            var frame = new EarthGestureFrame(
                grounded: grounded,
                descending: descending,
                moveMagnitude: move,
                modifierHeld: modifier,
                jumpPressed: true,
                landingWaveArmed: !grounded);

            Assert.That(EarthActionIntentResolver.Resolve(in frame).Kind, Is.EqualTo(expected));
        }

        [Test]
        public void NormalizedTapClassificationDoesNotDependOnPixelResolution()
        {
            var at1080p = new EarthGestureFrame(
                primaryReleased: true,
                primaryHeldSeconds: 0.12f,
                pointerTravelViewport: 12f / 1920f);
            var at4k = new EarthGestureFrame(
                primaryReleased: true,
                primaryHeldSeconds: 0.12f,
                pointerTravelViewport: 24f / 3840f);

            Assert.That(EarthActionIntentResolver.Resolve(in at1080p).Kind,
                Is.EqualTo(EarthActionIntentKind.QuickPrime));
            Assert.That(EarthActionIntentResolver.Resolve(in at4k).Kind,
                Is.EqualTo(EarthActionIntentKind.QuickPrime));
        }

        [Test]
        public void TargetManipulationBeatsQuickFireAndFullHold()
        {
            var frame = new EarthGestureFrame(
                primaryHeld: true,
                primaryHeldSeconds: 0.9f,
                forceHeld: true,
                hasControlledTarget: true,
                hasPrimedQuickStone: true);

            EarthActionIntent result = EarthActionIntentResolver.Resolve(in frame);

            Assert.That(result.Kind, Is.EqualTo(EarthActionIntentKind.ManipulateTarget));
            Assert.That(result.Consumes(EarthInputConsumption.Primary), Is.True);
            Assert.That(result.Consumes(EarthInputConsumption.Force), Is.True);
        }

        [Test]
        public void AuthoredGestureProfileKeepsItsScriptBinding()
        {
            EarthGestureProfile profile = AssetDatabase.LoadAssetAtPath<EarthGestureProfile>(
                "Assets/Elemental/Content/Profiles/EarthGestureProfile.asset");
            Assert.That(profile, Is.Not.Null,
                "A missing ScriptableObject binding silently disables the contextual gesture grammar in builds.");
        }
    }
}
