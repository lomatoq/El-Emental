using System.Collections.Generic;
using System.Linq;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class EarthFeedbackSignalPriorityTests
    {
        private GameObject owner;
        private EarthMaterialFeedbackHub hub;
        private readonly List<EarthMaterialFeedbackCue> presented=new List<EarthMaterialFeedbackCue>();
        [SetUp] public void Setup()
        {
            owner=new GameObject("Bounded signal queue fixture");
            hub=owner.AddComponent<EarthMaterialFeedbackHub>();hub.Configure(null,owner.transform);
            presented.Clear();hub.Presented+=presented.Add;
        }
        private void Emit(EarthMaterialFeedbackKind kind,uint source,Vector3 point,uint generation=1)
        {hub.Emit(kind,point,Vector3.up,1,.1f,source,generation,8,2,ElementId.Fire);}
        [TestCase(false)] [TestCase(true)]
        public void BothActorIgnitionsSurviveDustBeforeAndAfterAdmission(bool coLocated)
        {
            // Saturate with distinct near-player impacts. The far bot formerly lost
            // its event solely because all eight impact positions were nearer.
            for(uint i=0;i<8;i++)Emit(EarthMaterialFeedbackKind.Impact,100+i,new Vector3(i*2,0,0));
            Emit(EarthMaterialFeedbackKind.FireIgnite,1,Vector3.zero);
            Emit(EarthMaterialFeedbackKind.FireIgnite,2,coLocated?Vector3.zero:Vector3.forward*50);
            for(uint i=0;i<24;i++)Emit(EarthMaterialFeedbackKind.Impact,200+i,new Vector3(0,0,i*.01f));
            hub.FlushPending();
            var ignitions=presented.Where(x=>x.Kind==EarthMaterialFeedbackKind.FireIgnite).ToArray();
            Assert.That(ignitions.Select(x=>x.SourceId),Is.EquivalentTo(new uint[]{1,2}));
            Assert.That(ignitions.All(x=>x.Generation==1),Is.True);
            Assert.That(presented.Count,Is.LessThanOrEqualTo(8));
        }
        [Test] public void LifecycleSignalPreemptsContactAccentsWithoutGrowingTheQueue()
        {
            for(uint i=0;i<8;i++)Emit(EarthMaterialFeedbackKind.FireContact,100+i,new Vector3(i*2,0,0));
            Emit(EarthMaterialFeedbackKind.FireEnd,1,Vector3.forward*50);
            Emit(EarthMaterialFeedbackKind.FireEnd,2,Vector3.forward*50);
            for(uint i=0;i<20;i++)Emit(EarthMaterialFeedbackKind.FireContact,200+i,Vector3.zero);
            hub.FlushPending();
            Assert.That(presented.Where(x=>x.Kind==EarthMaterialFeedbackKind.FireEnd).Select(x=>x.SourceId),Is.EquivalentTo(new uint[]{1,2}));
            Assert.That(presented.Count,Is.EqualTo(8));
        }
        [Test] public void DistinctSignalFloodStillHonorsTheExistingEightEventCapacity()
        {
            for(uint i=0;i<200;i++)Emit(EarthMaterialFeedbackKind.SchoolSwitch,i+1,Vector3.zero,i+1);
            hub.FlushPending();Assert.That(presented.Count,Is.EqualTo(8));
        }
        [TearDown] public void Cleanup(){if(owner!=null)Object.DestroyImmediate(owner);}
    }
}
