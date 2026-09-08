using System.Reflection;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Structures;
using NUnit.Framework;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthRepairSeamCompletionTests
    {
        [TestCase(EarthBondPhase.Damaged,true,true,true,true)]
        [TestCase(EarthBondPhase.Healthy,true,true,true,true)]
        [TestCase(EarthBondPhase.Broken,true,true,true,true)]
        [TestCase(EarthBondPhase.Reforming,true,true,true,true)]
        [TestCase(EarthBondPhase.Damaged,false,true,true,false)]
        [TestCase(EarthBondPhase.Damaged,true,false,true,false)]
        [TestCase(EarthBondPhase.Damaged,true,true,false,false)]
        [TestCase(EarthBondPhase.Broken,false,true,true,false)]
        [TestCase(EarthBondPhase.Broken,true,false,true,false)]
        [TestCase(EarthBondPhase.Broken,true,true,false,false)]
        public void ExistingJointWithBothActuallySeatedEndpointsCanBeRewelded(EarthBondPhase phase,bool jointExists,
            bool firstSeated,bool secondSeated,bool expected)
        {
            var method=typeof(EarthReassemblyController).GetMethod("CanReconcileSeatedBond",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That((bool)method.Invoke(null,new object[]{phase,jointExists,firstSeated,secondSeated}),Is.EqualTo(expected));
        }
    }
}
