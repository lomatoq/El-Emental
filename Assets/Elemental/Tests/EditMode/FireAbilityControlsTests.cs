using Elemental.Simulation.Fire;
using NUnit.Framework;
using Unity.Mathematics;
namespace Elemental.Tests.EditMode
{
    public sealed class FireAbilityControlsTests
    {
        [Test] public void DoubleTapIsHandAndFurtherRapidTapIsFoot()
        {var s=new FireAbilityControls();Assert.That(s.Step(1,true,true,false,false,false,0).HandBolt,Is.False);Assert.That(s.Step(1.2,true,true,false,false,false,0).HandBolt,Is.True);Assert.That(s.Step(1.4,true,true,false,false,false,0).FootBolt,Is.True);Assert.That(s.Step(2,true,true,false,false,false,0).FootBolt,Is.False);}
        [Test] public void ShiftSpaceOwnsOneRingAndNeverLift()
        {var s=new FireAbilityControls();var a=s.Step(1,false,false,false,true,true,0);Assert.That(a.Ring,Is.True);Assert.That(a.Flight,Is.False);Assert.That(s.Step(1.1,false,false,false,true,true,0).Ring,Is.False);Assert.That(s.Step(1.2,false,false,false,true,false,0).Flight,Is.True);}
        [Test] public void DualMouseSwipeCommitsOnlyOnceAboveTravelThreshold()
        {var s=new FireAbilityControls();Assert.That(s.Step(1,true,true,true,false,false,0).LineBegin,Is.True);Assert.That(s.Step(1.1,false,true,true,false,false,new float2(50,0)).Drawing,Is.True);Assert.That(s.Step(1.2,false,false,true,false,false,new float2(50,0)).LineCommit,Is.True);Assert.That(s.Step(1.3,false,false,false,false,false,new float2(50,0)).LineCommit,Is.False);}
        [Test] public void ResetPreventsStaleSecondTapAndLiftChargeIsMonotonicBounded()
        {var s=new FireAbilityControls();s.Step(1,true,true,false,false,false,0);s.Reset();Assert.That(s.Step(1.1,true,true,false,false,false,0).HandBolt,Is.False);Assert.That(FireAbilityTuning.LiftCharge(0),Is.Zero);Assert.That(FireAbilityTuning.LiftCharge(1),Is.LessThan(FireAbilityTuning.LiftCharge(2)));Assert.That(FireAbilityTuning.LiftCharge(20),Is.EqualTo(1));}
        [TestCase(true)]
        [TestCase(false)]
        public void StaggeredButtonsOwnOneLineAndEitherReleaseCommits(bool primaryFirst)
        {
            var controls=new FireAbilityControls();
            var first=controls.Step(1,primaryFirst,primaryFirst,!primaryFirst,false,false,0);
            Assert.That(first.LineBegin||first.LineCommit||first.HandBolt||first.FootBolt,Is.False);
            Assert.That(controls.Step(1.05,!primaryFirst,true,true,false,false,0).LineBegin,Is.True);
            var dragging=controls.Step(1.1,false,true,true,false,false,new float2(40,0));
            Assert.That(dragging.Drawing,Is.True);
            var release=controls.Step(1.2,false,primaryFirst,!primaryFirst,false,false,new float2(40,0));
            Assert.That(release.LineCommit,Is.True);
            Assert.That(release.HandBolt||release.FootBolt,Is.False);
            Assert.That(controls.Step(1.3,false,false,false,false,false,new float2(40,0)).LineCommit,Is.False);
        }
        [Test] public void HeldRingOnlyReleasesOnChordRelease()
        {
            var s=new FireAbilityControls();s.Step(1,false,false,false,true,true,0);
            Assert.That(s.Step(20,false,false,false,true,true,0).RingReleased,Is.False);
            Assert.That(s.Step(21,false,false,false,false,true,0).RingReleased,Is.True);
            Assert.That(s.Step(22,false,false,false,false,false,0).RingReleased,Is.False);
        }
        [Test] public void ChordClickFiresOnceWhileReturningSwipeRemainsLine()
        {
            var s=new FireAbilityControls();s.Step(1,true,true,true,false,false,0);
            var click=s.Step(1.1,false,false,true,false,false,1);Assert.That(click.HandBolt,Is.True);Assert.That(click.LineCommit,Is.False);
            Assert.That(s.Step(1.2,false,false,false,false,false,1).HandBolt,Is.False);
            s.Step(2,true,true,true,false,false,0);s.Step(2.1,false,true,true,false,false,new float2(40,0));
            var back=s.Step(2.2,false,false,false,false,false,0);Assert.That(back.LineCommit,Is.True);Assert.That(back.HandBolt,Is.False);
            s.Step(3,true,true,true,false,false,0);s.Reset();Assert.That(s.Step(3.1,false,false,false,false,false,0).HandBolt,Is.False);
        }
        [Test] public void RingFrontSlowsWhileRemainingMonotonicAndReachesFullExtent()
        {
            float previous=0;
            for(int i=0;i<=100;i++){float extent=FireAbilityTuning.RingExtent(i*FireAbilityTuning.RingSeconds/100);Assert.That(extent,Is.GreaterThanOrEqualTo(previous));previous=extent;}
            Assert.That(previous,Is.EqualTo(FireAbilityTuning.RingRadius).Within(.001f));
            Assert.That(FireAbilityTuning.RingSpeed(.1f),Is.GreaterThan(FireAbilityTuning.RingSpeed(.5f)));
            Assert.That(FireAbilityTuning.RingSpeed(FireAbilityTuning.RingSeconds),Is.Zero);
        }
    }
}
