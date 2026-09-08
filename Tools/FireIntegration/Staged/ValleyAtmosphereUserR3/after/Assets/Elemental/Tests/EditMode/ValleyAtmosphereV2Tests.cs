using System;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public static class ValleyAtmosphereV2TestLauncher
    {
        [UnityEditor.MenuItem("Elemental/QA/Run Valley Atmosphere V2 Tests")]
        public static void Run()
        {
            var run=typeof(Mvp01FocusedTestLauncher).GetMethod("Run",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            run.Invoke(null,new object[]{UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,"ValleyAtmosphereV2Edit",new[]{"Elemental.Tests.EditMode.ValleyAtmosphereV2Tests"}});
        }
    }
    public sealed class ValleyAtmosphereV2Tests
    {
        [TestCase(150,150)][TestCase(150,-120)][TestCase(-150,120)][TestCase(-150,-120)][TestCase(0,0)][TestCase(130,130.00001)]
        public void IntegralMatchesIndependentNumericalQuadrature(double start,double end)
        {
            const int count=20000;const double length=1700,falloff=32;double sum=0;
            for(int i=0;i<count;i++){double h=start+(end-start)*(i+0.5)/count;sum+=Math.Exp(-Math.Max(h,0)/falloff);}
            double reference=sum*length/count;
            Assert.That(ValleyAtmosphereMath.IntegratedDensity(start,end,length,falloff),Is.EqualTo(reference).Within(Math.Max(0.00001,reference*0.000001)));
        }
        [Test]
        public void NoBlueHoleForShallowDownwardSkyAndNoFarClipDependency()
        {
            foreach(double slope in new[]{-1.0,-0.01,-0.00001,0.0})Assert.That(ValleyAtmosphereMath.SkyOpacity(150,slope,32,0.025),Is.EqualTo(1));
            Assert.That(ValleyAtmosphereMath.SkyOpacity(150,0.00001,32,0.025),Is.GreaterThan(0.999));
            Assert.That(ValleyAtmosphereMath.SkyOpacity(150,1,32,0.025),Is.LessThan(0.02));
        }
        [Test]
        public void FarStonePerspectiveIsContinuousMonotonicAndNearClear()
        {
            Assert.That(ValleyAtmosphereMath.FarOpacity(299,300,1800,0.6),Is.Zero);
            double previous=0;for(int d=300;d<=5000;d++){double current=ValleyAtmosphereMath.FarOpacity(d,300,1800,0.6);Assert.That(current,Is.InRange(previous,0.6));Assert.That(current-previous,Is.LessThan(0.001));previous=current;}
        }
        [Test]
        public void ExteriorReverseViewStillProtectsPlanetFromCloudArtwork()
        {
            // Camera2200m away; bank690m away passes near-plane guard, but planet
            // surface must retain its protected original color behind that bank.
            Assert.That(ValleyAtmosphereMath.OpaqueProtection(2145,55.1,55.1,300),Is.Zero);
            Assert.That(ValleyAtmosphereMath.OpaqueProtection(299,800,55.1,300),Is.Zero);
            Assert.That(ValleyAtmosphereMath.OpaqueProtection(1000,800,55.1,300),Is.EqualTo(1));
        }
        [Test]
        public void SubmergedObserverCannotSeeBrightArtThroughOpaqueVeil()
        {
            Assert.That(ValleyAtmosphereMath.CloudTransmittance(-50,0,1500,32,0.025),Is.LessThan(1e-12));
            Assert.That(ValleyAtmosphereMath.CloudTransmittance(250,250,650,32,0.025),Is.GreaterThan(0.99));
        }
        [Test]
        public void CloudTransmissionMatchesOneMinusVeilOpacity()
        {
            foreach(double end in new[]{-120.0,0.0,35.0,180.0})
                Assert.That(ValleyAtmosphereMath.CloudTransmittance(130,end,900,32,0.025)+ValleyAtmosphereMath.Opacity(130,end,900,32,0.025),Is.EqualTo(1).Within(1e-12));
        }
        [Test]
        public void LowerPlanetIsOpaqueButPlayableUpperCapRemainsProtected()
        {
            const double radius=55.1;
            Assert.That(ValleyAtmosphereMath.UpperWindowProtection(10,radius,radius,radius,300),Is.Zero);
            Assert.That(ValleyAtmosphereMath.UpperWindowProtection(2200,radius,radius,radius,300),Is.Zero);
            Assert.That(ValleyAtmosphereMath.UpperWindowProtection(10,radius,-radius,radius,300),Is.EqualTo(1));
            Assert.That(ValleyAtmosphereMath.LowerTerrainOpacity(-radius,radius),Is.EqualTo(1));
            Assert.That(ValleyAtmosphereMath.LowerTerrainOpacity(radius,radius),Is.Zero);
            double previous=1;
            for(double height=-radius;height<=radius;height+=0.05)
            {double value=ValleyAtmosphereMath.LowerTerrainOpacity(height,radius);Assert.That(value,Is.InRange(0,previous+1e-12));Assert.That(previous-value,Is.LessThan(0.003));previous=value;}
        }
        [Test]
        public void InvalidInputsDoNotLeakNanIntoShaderParameters()
        {Assert.Throws<ArgumentOutOfRangeException>(()=>ValleyAtmosphereMath.IntegratedDensity(double.NaN,0,100,32));Assert.Throws<ArgumentOutOfRangeException>(()=>ValleyAtmosphereMath.IntegratedDensity(0,0,100,0));}
    }
}
