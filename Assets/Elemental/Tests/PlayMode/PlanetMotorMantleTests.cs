using System.Collections;
using System.Reflection;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class PlanetMotorPlayModeTests
    {
        [UnityTest]
        public IEnumerator AutoMantle_ForwardIntentClimbsBroadLedgeAndLands()
        {
            Fixture fixture = CreateFixture(Vector3.up);
            GameObject ledge = MantleTestLedge(fixture, .9f);
            try
            {
                for (int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                float startHeight = fixture.Body.position.y;
                fixture.Input.Move = new float2(0,1);
                for (int i=0;i<100 && fixture.Motor.MantleSequence==0;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling, Is.True, "Forward intent must automatically begin a reachable climb.");
                fixture.Input.Move = float2.zero;
                for (int i=0;i<150;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling, Is.False);
                Assert.That(fixture.Motor.MantleLastRejection, Is.Null);
                Assert.That(fixture.Body.position.y-startHeight, Is.GreaterThan(.6f));
                Assert.That(fixture.Motor.HasStableSupport, Is.True);
                AssertFinite(fixture.Body.position);
            }
            finally { Object.Destroy(ledge); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator AutoMantle_RejectsHighLedgeAndBlockedHeadroom()
        {
            for (int scenario=0;scenario<2;scenario++)
            {
                Fixture fixture = CreateFixture(Vector3.up);
                GameObject ledge = MantleTestLedge(fixture, scenario==0 ? 2f : .9f);
                GameObject ceiling = null;
                if(scenario==1)
                {
                    ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ceiling.transform.position=fixture.Center+new Vector3(0,12.6f,2.4f);
                    ceiling.transform.localScale=new Vector3(4,.2f,3);
                }
                try
                {
                    for(int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                    fixture.Input.Move=new float2(0,1);
                    for(int i=0;i<100;i++) yield return new WaitForFixedUpdate();
                    Assert.That(fixture.Motor.MantleSequence, Is.Zero, "Unsafe destination must not start mantle: scenario "+scenario);
                }
                finally { if(ceiling!=null) Object.Destroy(ceiling); Object.Destroy(ledge); DestroyFixture(fixture); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AutoMantle_DestroyedSupportInterruptsTraversal()
        {
            Fixture fixture=CreateFixture(Vector3.up);
            GameObject ledge=MantleTestLedge(fixture,.9f);
            try
            {
                for(int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                fixture.Input.Move=new float2(0,1);
                for(int i=0;i<100 && fixture.Motor.MantleSequence==0;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling, Is.True);
                fixture.Input.Move=float2.zero;
                ledge.SetActive(false);
                yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling, Is.False);
                Assert.That(fixture.Motor.MantleLastRejection, Does.Contain("Support"));
                AssertFinite(fixture.Body.linearVelocity);
            }
            finally { Object.Destroy(ledge); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator AutoMantle_SameColliderLosingFootprintStopsGravityCancellation()
        {
            Fixture fixture=CreateFixture(Vector3.up);
            GameObject ledge=MantleTestLedge(fixture,.9f);
            try
            {
                for(int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                fixture.Input.Move=new float2(0,1);
                for(int i=0;i<100&&fixture.Motor.MantleSequence==0;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling,Is.True);
                fixture.Input.Move=float2.zero;
                for(int i=0;i<80&&fixture.Motor.IsMantling&&fixture.Motor.MantleProgress<.45f;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling,Is.True);
                float height=fixture.Body.position.y;
                // Keep the collider reference/generation alive but remove its
                // landing footprint, as a mesh edit can do without proxy swap.
                BoxCollider shape=ledge.GetComponent<BoxCollider>();
                shape.size=new Vector3(.05f,shape.size.y,.05f);
                UnityEngine.Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling,Is.False);
                Assert.That(fixture.Motor.MantleLastRejection,Does.Contain("Support"));
                for(int i=0;i<60;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Body.position.y,Is.LessThan(height-.1f),"Aborted mantle must not hover at its last path goal.");
                AssertFinite(fixture.Body.position);
            }
            finally { Object.Destroy(ledge); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator AutoMantle_ReleasedSupportAndNewReachOverlapAbort()
        {
            for(int scenario=0;scenario<2;scenario++)
            {
                Fixture fixture=CreateFixture(Vector3.up);
                GameObject ledge=MantleTestLedge(fixture,.9f);
                GameObject obstruction=null;
                try
                {
                    for(int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                    fixture.Input.Move=new float2(0,1);
                    for(int i=0;i<100&&fixture.Motor.MantleSequence==0;i++) yield return new WaitForFixedUpdate();
                    Assert.That(fixture.Motor.IsMantling,Is.True);
                    fixture.Input.Move=float2.zero;
                    if(scenario==0)
                    {
                        Rigidbody released=ledge.AddComponent<Rigidbody>();
                        released.useGravity=false; released.isKinematic=false;
                    }
                    else
                    {
                        // Capsule sweeps do not detect a collider already overlapping
                        // their starting capsule, especially during the stationary reach.
                        obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);
                        obstruction.transform.position=fixture.Body.position+fixture.Motor.LocalUp*.45f;
                        obstruction.transform.localScale=Vector3.one*.2f;
                    }
                    UnityEngine.Physics.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    Assert.That(fixture.Motor.IsMantling,Is.False,"Traversal must abort scenario "+scenario);
                    Assert.That(fixture.Motor.MantleLastRejection,Does.Contain(scenario==0 ? "Support" : "obstructed"));
                    AssertFinite(fixture.Body.linearVelocity);
                }
                finally { if(obstruction!=null) Object.Destroy(obstruction); Object.Destroy(ledge); DestroyFixture(fixture); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AutoMantle_EquatorialForwardIntentClimbsWithRadialUp()
        {
            Fixture fixture=CreateFixture(Vector3.right);
            GameObject ledge=MantleTestLedge(fixture,.9f);
            try
            {
                for(int i=0;i<50;i++) yield return new WaitForFixedUpdate();
                Vector3 start=fixture.Body.position;
                fixture.Input.Move=new float2(0,1);
                for(int i=0;i<100&&fixture.Motor.MantleSequence==0;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling,Is.True,"Equatorial mantle must use local gravity up, not world Y.");
                fixture.Input.Move=float2.zero;
                for(int i=0;i<150;i++) yield return new WaitForFixedUpdate();
                Assert.That(fixture.Motor.IsMantling,Is.False);
                Assert.That(fixture.Motor.MantleLastRejection,Is.Null);
                Assert.That(Vector3.Dot(fixture.Body.position-start,Vector3.right),Is.GreaterThan(.6f));
                Assert.That(fixture.Motor.HasStableSupport,Is.True);
            }
            finally { Object.Destroy(ledge); DestroyFixture(fixture); }
        }

        [UnityTest]
        public IEnumerator AutoMantle_FootprintAcceptsThirtyDegreeSlopeAndRejectsSteepSlope()
        {
            Fixture fixture=CreateFixture(Vector3.up);
            GameObject surface=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                MethodInfo check=typeof(PlanetMotor).GetMethod("MantleFootprintSupported",BindingFlags.NonPublic|BindingFlags.Instance);
                Assert.That(check,Is.Not.Null);
                Vector3 top=fixture.Center+Vector3.up*20f;
                foreach(float degrees in new[] { 30f,55f })
                {
                    surface.transform.rotation=Quaternion.AngleAxis(degrees,Vector3.right);
                    surface.transform.localScale=new Vector3(4f,.4f,4f);
                    surface.transform.position=top-surface.transform.up*.2f;
                    UnityEngine.Physics.SyncTransforms();
                    var support=CharacterSupportRuntimeAdapter.Classify(surface.GetComponent<Collider>(),0,Vector3.Dot(surface.transform.up,Vector3.up));
                    bool accepted=(bool)check.Invoke(fixture.Motor,new object[] { top,Vector3.forward,.5f,Vector3.up,
                        Mathf.Cos(45f*Mathf.Deg2Rad),support.SurfaceId,support.Generation });
                    Assert.That(accepted,Is.EqualTo(degrees==30f),"Actual footprint rays on "+degrees+" degree surface.");
                    yield return null;
                }
            }
            finally { Object.Destroy(surface); DestroyFixture(fixture); }
        }

        private static GameObject MantleTestLedge(Fixture fixture,float height)
        {
            var ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name="Automatic mantle test ledge";
            Quaternion radialFrame=Quaternion.FromToRotation(Vector3.up,fixture.RadialUp);
            ledge.transform.rotation=radialFrame;
            ledge.transform.position=fixture.Center+radialFrame*new Vector3(0,9f+height*.5f,2.4f);
            ledge.transform.localScale=new Vector3(4,2f+height,3);
            return ledge;
        }
    }
}
