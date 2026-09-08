using NUnit.Framework;
using UnityEngine;
using Elemental.Presentation.VFX;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthGroundDustDensityTests
    {
        [Test]
        public void NeighbourhoodWeightsPreferGapsOverAnIsolatedStone()
        {
            var p=new[]{Vector3.zero,Vector3.right,Vector3.forward,new Vector3(12,0,0)};
            var n=new int[4];var nearest=new int[4];var w=new float[4];
            EarthGroundDustDensity.Evaluate(p,4,Vector3.up,3.5f,.2f,4,n,nearest,w,out float mean);
            Assert.That(n,Is.EqualTo(new[]{2,2,2,0}));Assert.That(nearest[3],Is.EqualTo(-1));
            Assert.That(mean,Is.EqualTo(1.5f));Assert.That(w[0],Is.GreaterThan(w[3]*2));
            int clustered=0,isolated=0;
            for(int i=0;i<1000;i++){int index=EarthGroundDustDensity.Select(w,4,(i+.5f)/1000);if(index==3)isolated++;else clustered++;}
            Assert.That(clustered,Is.GreaterThan(isolated*5));
        }
        [Test]
        public void OverheadStonesDoNotCreateFalseGroundDensityAndEmptySelectionIsSafe()
        {
            var p=new[]{Vector3.zero,new Vector3(0,5,0)};var n=new int[2];var nearest=new int[2];var w=new float[2];
            EarthGroundDustDensity.Evaluate(p,2,Vector3.up,3.5f,.2f,4,n,nearest,w,out float mean);
            Assert.That(mean,Is.Zero);Assert.That(nearest,Is.EqualTo(new[]{-1,-1}));
            Assert.That(EarthGroundDustDensity.Select(w,0,.5f),Is.EqualTo(-1));
        }
        [Test]
        public void RotatingTheGroundFrameKeepsDensityEquivalent()
        {
            var p=new[]{Vector3.zero,Vector3.right,Vector3.forward};var n=new int[3];var nearest=new int[3];var w=new float[3];
            EarthGroundDustDensity.Evaluate(p,3,Vector3.up,3,.2f,4,n,nearest,w,out float a);
            var q=Quaternion.Euler(37,51,23);for(int i=0;i<p.Length;i++)p[i]=q*p[i]+new Vector3(300,-40,90);
            EarthGroundDustDensity.Evaluate(p,3,q*Vector3.up,3,.2f,4,n,nearest,w,out float b);
            Assert.That(b,Is.EqualTo(a));Assert.That(n,Is.EqualTo(new[]{2,2,2}));
        }
    }
}
