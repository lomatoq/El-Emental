using Elemental.Simulation.Rendering;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthDustSupportPolicyTests
    {
        [Test] public void OldestReservationsCannotBeStarvedByAVisibleEdge()
        {
            float[] age={.45f,.1f,.2f};
            float[] score={1,10,2}; bool[] selected=new bool[3];
            Assert.That(EarthDustSupportPolicy.Select(age,score,selected,3,true),Is.EqualTo(0));
            selected[0]=true;
            Assert.That(EarthDustSupportPolicy.Select(age,score,selected,3,false),Is.EqualTo(1));
            selected[1]=true;
            Assert.That(EarthDustSupportPolicy.Select(age,score,selected,3,false),Is.EqualTo(2));
            selected[2]=true;
            Assert.That(EarthDustSupportPolicy.Select(age,score,selected,3,false),Is.EqualTo(-1));
        }
        [Test] public void StaleSupportLosesConfidenceAndOutranksRecentVisibleEdges()
        {
            Assert.That(EarthDustSupportPolicy.Confidence(.2f),Is.EqualTo(1));
            Assert.That(EarthDustSupportPolicy.Confidence(.55f),Is.EqualTo(0).Within(.00001f));
            Assert.That(EarthDustSupportPolicy.Priority(.5f,false,0,10000),
                Is.GreaterThan(EarthDustSupportPolicy.Priority(.1f,true,1,0)));
        }
        [Test] public void RetiredCandidatesAreNeverSelected()
        {
            Assert.That(EarthDustSupportPolicy.Select(new[]{1f},new[]{-1f},new[]{false},1,true),Is.EqualTo(-1));
        }
    }
}
