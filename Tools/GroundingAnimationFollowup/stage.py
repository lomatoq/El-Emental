from pathlib import Path
root = Path(__file__).resolve().parents[2]
out = Path(__file__).parent / 'after'
def change(path, edits):
    s=(root/path).read_text(encoding='utf-8-sig')
    for old,new in edits:
        assert old in s, (path, old[:100])
        s=s.replace(old,new,1)
    p=out/path; p.parent.mkdir(parents=True,exist_ok=True); p.write_text(s,encoding='utf-8')
def new(path,s):
    p=out/path; p.parent.mkdir(parents=True,exist_ok=True); p.write_text(s,encoding='utf-8')
change('Assets/Elemental/Simulation/Characters/CharacterSupportAuthority.cs',[
('DynamicDebris = 5','DynamicDebris = 5,\n        SettledMatter = 6'),
('// make a released fragment or debris collider become ground.','// grant support to moving debris that failed physical admission.'),
('candidate.Kind == CharacterSupportKind.MovingAbilitySurface;','candidate.Kind == CharacterSupportKind.MovingAbilitySurface ||\n                   candidate.Kind == CharacterSupportKind.SettledMatter;'),
('            int candidatePriority = Priority(candidate.Kind);','''            // Semantic priority resolves near-coincident proxy seams only.
            // A buried arena collider must not beat the actual top of a rock.
            if (math.abs(candidate.Distance - incumbent.Distance) > 0.02f)
                return candidate.Distance < incumbent.Distance;
            int candidatePriority = Priority(candidate.Kind);'''),
('CharacterSupportKind.ArenaWalkableProxy => 3,','CharacterSupportKind.SettledMatter => 4,\n            CharacterSupportKind.ArenaWalkableProxy => 3,')])
new('Assets/Elemental/Simulation/Characters/SettledMatterSupportPolicy.cs','''using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Sleeping admits a physical support; small contact wakes may retain it.
    /// Slow velocity alone cannot admit a stone at the apex of a throw.</summary>
    public static class SettledMatterSupportPolicy
    {
        public static bool CanSupport(bool sleeping, bool previouslySettled,
            bool kinematic, float linearSpeedSquared, float angularSpeedSquared)
        {
            if (kinematic || !math.isfinite(linearSpeedSquared) ||
                !math.isfinite(angularSpeedSquared)) return false;
            return sleeping || previouslySettled && linearSpeedSquared <= 0.15f * 0.15f &&
                angularSpeedSquared <= 0.35f * 0.35f;
        }
    }
}
''')
change('Assets/Elemental/Runtime/Characters/CharacterSupportRuntimeAdapter.cs',[
('using Elemental.Runtime.Physics;','using Elemental.Runtime.Physics;\nusing Elemental.Runtime.Matter;\nusing Elemental.Simulation.Matter;'),
('float upDot)\n        {','float upDot,\n            CharacterSupportSelection previous = default,\n            CharacterSupportSelection sharedSupport = default)\n        {'),
('                EarthPhysicalTargetHandle handle = physicalTarget.TargetHandle;','''                EarthPhysicalTargetHandle handle = physicalTarget.TargetHandle;
                if (CanSupportSettledMatter(collider, physicalTarget, in previous, in sharedSupport))
                    return Candidate(handle.StableId, handle.Generation,
                        CharacterSupportKind.SettledMatter, distance, upDot, true);'''),
('        private static CharacterSupportCandidate Candidate(','''        private static bool CanSupportSettledMatter(Collider collider,
            IEarthPhysicalTarget target, in CharacterSupportSelection previous,
            in CharacterSupportSelection sharedSupport)
        {
            if (!target.IsEarthTargetValid || !target.TargetHandle.IsValid ||
                target.TargetKind is not (EarthPhysicalTargetKind.Rock or
                    EarthPhysicalTargetKind.WallPiece or EarthPhysicalTargetKind.PlatformPiece))
                return false;
            Rigidbody body = target.Body;
            if (body == null || collider.attachedRigidbody != body) return false;
            // Authored anchored decor is real collision geometry, including before
            // its first fracture; kinematic magic-held matter is not a support.
            EarthDestructibleDecorRock decor = target as EarthDestructibleDecorRock;
            if (decor != null && decor.IsAnchored) return true;
            EarthMatterIdentity identity = body.GetComponent<EarthMatterIdentity>();
            if (identity != null && identity.TryRead(out EarthMatterRecord record) &&
                record.Phase is not (EarthMatterPhase.FreeDynamic or EarthMatterPhase.Sleeping))
                return false;
            bool retained = MatchesSettled(in previous, target.TargetHandle) ||
                MatchesSettled(in sharedSupport, target.TargetHandle);
            return SettledMatterSupportPolicy.CanSupport(body.IsSleeping(), retained,
                body.isKinematic, body.linearVelocity.sqrMagnitude, body.angularVelocity.sqrMagnitude);
        }

        private static bool MatchesSettled(in CharacterSupportSelection previous,
            EarthPhysicalTargetHandle handle) => previous.HasSupport &&
            previous.Candidate.Kind == CharacterSupportKind.SettledMatter &&
            previous.Candidate.SurfaceId == handle.StableId &&
            previous.Candidate.Generation == handle.Generation;

        private static CharacterSupportCandidate Candidate(''')])
change('Assets/Elemental/Runtime/Characters/PlanetMotor.cs',[
('GroundHitCapacity = 8','GroundHitCapacity = 32'),
('                    slopeDot);','                    slopeDot, previous);')])
change('Assets/Elemental/Presentation/Animation/EarthFootContactController.cs',[
('FootHitCapacity = 8','FootHitCapacity = 32'),
('                    upDot);','                    upDot, previousSupport, motor.GroundSupport);')])
new('Assets/Elemental/Tests/EditMode/SettledMatterSupportTests.cs','''using Elemental.Simulation.Characters;
using NUnit.Framework;

namespace Elemental.Tests.EditMode
{
    public sealed class SettledMatterSupportTests
    {
        [Test] public void ApexCannotBecomeSupportButSleepingRubbleCan()
        {
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, false, false, 0f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(true, false, false, 0f, 0f), Is.True);
        }
        [Test] public void ContactWakeRetainsButThrowAndKinematicGrabRelease()
        {
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, .01f, .04f), Is.True);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, 1f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, true, 0f, 0f), Is.False);
            Assert.That(SettledMatterSupportPolicy.CanSupport(false, true, false, 0f, 1f), Is.False);
        }
        [Test] public void ActualNearerRockBeatsBuriedArenaRegardlessOfCandidateOrder()
        {
            var rock = new CharacterSupportCandidate(2, 3, CharacterSupportKind.SettledMatter, .1f, 1, true, true);
            var floor = new CharacterSupportCandidate(1, 1, CharacterSupportKind.ArenaWalkableProxy, .5f, 1, true, true);
            foreach (var candidates in new[] { new[] { floor, rock }, new[] { rock, floor } })
                Assert.That(CharacterSupportAuthority.Select(candidates, 2, default, .55f, .035f).Candidate.SurfaceId, Is.EqualTo(2));
        }
        [Test] public void ActualNearerPlatformBeatsBuriedArena()
        {
            var platform = new CharacterSupportCandidate(2, 3, CharacterSupportKind.MovingAbilitySurface, .1f, 1, true, true);
            var floor = new CharacterSupportCandidate(1, 1, CharacterSupportKind.ArenaWalkableProxy, .5f, 1, true, true);
            Assert.That(CharacterSupportAuthority.Select(new[] { floor, platform }, 2, default, .55f, .035f).Candidate.SurfaceId, Is.EqualTo(2));
        }
    }
}
''')

# Select the nearest geometric band in a separate pass. Pairwise fuzzy sorting
# is not transitive and would reintroduce order-dependent nonalloc results.
p=out/'Assets/Elemental/Simulation/Characters/CharacterSupportAuthority.cs'
s=p.read_text()
s=s.replace('            int bestIndex = -1;', '''            float nearestDistance = float.MaxValue;
            for (int index = 0; index < safeCount; index++)
            {
                CharacterSupportCandidate candidate = candidates[index];
                if (CanOwnCharacterSupport(in candidate, minimumUp))
                    nearestDistance = math.min(nearestDistance, candidate.Distance);
            }
            int bestIndex = -1;''')
s=s.replace('                if (bestIndex < 0 || IsPreferred(', '''                if (candidate.Distance > nearestDistance + 0.02f) continue;
                if (bestIndex < 0 || IsPreferred(''')
s=s.replace('''            // Semantic priority resolves near-coincident proxy seams only.
            // A buried arena collider must not beat the actual top of a rock.
            if (math.abs(candidate.Distance - incumbent.Distance) > 0.02f)
                return candidate.Distance < incumbent.Distance;
''','')
p.write_text(s,encoding='utf-8')

change('Assets/Elemental/Simulation/Characters/EarthTurnStepSequence.cs',[
('bool eligible, bool turnClockVisible, float normalizedTime)',
 'bool eligible, bool turnClockVisible, float normalizedTime, float deltaTime = 1f / 60f)'),
('if (request == 0f && math.abs(measuredYawDelta) >= .5f)',
 '''// Compare angular speed, not degrees per rendered frame. The former
            // .5 degree gate rejected the same bot turn at 60/120 Hz but not 30 Hz.
            if (request == 0f && math.abs(measuredYawDelta) >= 7f * math.max(.0001f, deltaTime))''')])
change('Assets/Elemental/Presentation/Animation/HumanoidCharacterPresentation.cs',[
('canTakeTurnStep, turnClockVisible, turnClock.normalizedTime);',
 'canTakeTurnStep, turnClockVisible, turnClock.normalizedTime, Time.deltaTime);')])
change('Assets/Elemental/Tests/EditMode/EarthTurnStepSequenceTests.cs',[
('        [Test]\n        public void RealUncommandedYawTriggersButAlternatingSupportNoiseDoesNot()',
 '''        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SlowRealBotYawTriggersAtTheSameAngleAcrossRenderRates(int fps)
        {
            var state = new EarthTurnStepState();
            float angle = 0f;
            for (int i = 0; i < fps && !state.Active; i++)
            {
                angle += 20f / fps;
                EarthTurnStepSequence.Step(ref state, 0, 20f / fps, true, false, 0, 1f / fps);
            }
            Assert.That(state.Active, Is.True);
            Assert.That(angle, Is.InRange(5f, 5f + 20f / fps + .001f));
        }

        [Test]
        public void RealUncommandedYawTriggersButAlternatingSupportNoiseDoesNot()''')])

new('Assets/Elemental/Tests/PlayMode/PlanetMotorSettledMatterTests.cs','''using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class PlanetMotorPlayModeTests
    {
        [UnityTest]
        public IEnumerator MotorUsesActualAnchoredRockTopInsteadOfFloorBelow()
        {
            Fixture fixture = CreateFixture(Vector3.up, new Vector3(460f, 0f, 0f), true);
            var rockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                rockObject.name = "Actual decor support above terrain";
                rockObject.transform.position = fixture.Center + Vector3.up * 10.2f;
                rockObject.transform.localScale = new Vector3(3f, .4f, 3f);
                var rockBody = rockObject.AddComponent<Rigidbody>();
                rockBody.useGravity = false;
                var rock = rockObject.AddComponent<EarthDestructibleDecorRock>();
                // This fixture exercises support, not fracture. The actual rock
                // contract remains bound; omit its unrelated pool-start work.
                rock.enabled = false;
                rock.Configure(0xD3ABC001, rockBody, rockObject.GetComponent<Collider>(), null,
                    null, 1f, 100000f, true);
                fixture.Body.position += Vector3.up * .5f;
                Physics.SyncTransforms();
                for (int i = 0; i < 80; i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsGrounded, Is.True);
                Assert.That(fixture.Motor.GroundSupport.Candidate.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                Assert.That(fixture.Motor.GroundSupport.Candidate.SurfaceId, Is.EqualTo(rock.StableEarthId));
                Assert.That(Vector3.Dot(fixture.Motor.SupportFeetPoint(Vector3.up) -
                    (rockObject.transform.position + Vector3.up * .2f), Vector3.up), Is.InRange(-.03f, .05f));
            }
            finally { Object.Destroy(rockObject); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator ReleasedEarthRockSupportsAfterSleepAndKeepsIdentityAcrossContactWake()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = new Vector3(480, 20, 0);
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                var shape = go.GetComponent<Collider>();
                var rock = go.AddComponent<EarthDestructibleDecorRock>();
                rock.enabled = false;
                rock.Configure(0xD3ABC002, body, shape, null, null, .5f, 100000, false);
                body.Sleep();
                var sleeping = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f);
                Assert.That(sleeping.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                var prior = new CharacterSupportSelection(true, sleeping, false);
                body.WakeUp(); body.linearVelocity = Vector3.right * .05f;
                var awake = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f, prior);
                Assert.That(awake.Kind, Is.EqualTo(CharacterSupportKind.SettledMatter));
                Assert.That(awake.Generation, Is.EqualTo(rock.TargetHandle.Generation));
                body.linearVelocity = Vector3.right * 3f;
                var thrown = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f, prior);
                Assert.That(thrown.IsWalkable, Is.False);
                body.linearVelocity = Vector3.zero;
                var apex = CharacterSupportRuntimeAdapter.Classify(shape, .01f, 1f);
                Assert.That(apex.IsWalkable, Is.False);
                yield return null;
            }
            finally { Object.Destroy(go); }
        }
    }
}
''')
