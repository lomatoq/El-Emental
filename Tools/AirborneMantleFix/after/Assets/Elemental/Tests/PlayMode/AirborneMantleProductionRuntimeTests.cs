using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator AirborneForwardJumpCatchesAndMantlesRisingPlatformWithRealHands()
        {
            Actor actor=_actors.Find(value=>value.Presentation.GetComponent<EarthCharacterPoseController>()!=null);
            Assert.That(actor,Is.Not.Null);
            PlanetMotor motor=actor.Presentation.GetComponentInParent<PlanetMotor>();
            Animator animator=actor.Presentation.Animator;
            Assert.That(motor,Is.Not.Null);
            Assert.That(animator!=null&&animator.isHuman,Is.True);

            var input=motor.gameObject.AddComponent<AirborneMantleProofInput>();
            motor.ConfigureInputSource(input);
            motor.CancelMantle();
            Vector3 up=motor.LocalUp.normalized;
            Vector3 forward=Vector3.ProjectOnPlane(motor.FacingForward,up).normalized;
            Vector3 feet=motor.SupportFeetPoint(up);
            GameObject ledge=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name="Airborne mantle rising-platform proof";
            SceneManager.MoveGameObjectToScene(ledge,_scene);
            ledge.transform.SetPositionAndRotation(
                feet+forward*2.3f+up*.2f,
                Quaternion.LookRotation(forward,up));
            ledge.transform.localScale=new Vector3(4f,1.4f,3f);
            for(int layer=0;layer<32;layer++)
                if((motor.GroundMask.value&(1<<layer))!=0){ledge.layer=layer;break;}
            Rigidbody ledgeBody=ledge.AddComponent<Rigidbody>();
            ledgeBody.useGravity=false;
            ledgeBody.isKinematic=true;
            var moving=ledge.AddComponent<AirborneMantleProofSurface>();
            moving.Configure(ledgeBody,up*.12f,up);
            UnityEngine.Physics.SyncTransforms();

            try
            {
                uint sequence=motor.MantleSequence;
                input.Move=new float2(0f,1f);
                input.JumpQueued=true;
                bool sawAirborneBeforeMantle=false;
                double deadline=Time.realtimeSinceStartupAsDouble+6d;
                while(motor.MantleSequence==sequence&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return _frame;
                    sawAirborneBeforeMantle|=!motor.HasStableSupport;
                    Assert.That(
                        actor.Presentation.CurrentAuthoredAction==EarthAuthoredActionId.Mantle,
                        Is.EqualTo(motor.IsMantling),
                        "Presentation may show the climb only while the motor owns a real mantle path.");
                }

                Assert.That(sawAirborneBeforeMantle,Is.True,"The real jump never entered an airborne state.");
                Assert.That(motor.IsMantling,Is.True,
                    "Airborne ledge catch did not acquire the rising platform: "+motor.MantleLastRejection);
                Assert.That(motor.MantleStartedAirborne,Is.True);
                input.Move=float2.zero;
                Vector3 supportAtCatch=ledge.transform.position;
                float closestHand=float.PositiveInfinity;
                bool sawHandOwnership=false;
                bool sawRaise=false,sawTransfer=false,sawSettle=false;
                deadline=Time.realtimeSinceStartupAsDouble+5d;
                while(motor.IsMantling&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return _frame;
                    if(!motor.IsMantling)break;
                    Assert.That(actor.Presentation.CurrentAuthoredAction,Is.EqualTo(EarthAuthoredActionId.Mantle));
                    sawRaise|=motor.MantlePhase==EarthMantlePhase.Raise;
                    sawTransfer|=motor.MantlePhase==EarthMantlePhase.Transfer;
                    sawSettle|=motor.MantlePhase==EarthMantlePhase.Settle;
                    if(motor.MantleProgress>.20f&&motor.MantleProgress<.50f)
                    {
                        Vector3 right=Vector3.Cross(motor.LocalUp,motor.FacingForward).normalized;
                        Vector3 target=motor.MantleLedgePoint+right*.18f+motor.LocalUp*.025f;
                        Transform hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                        closestHand=Mathf.Min(closestHand,Vector3.Distance(hand.position,target));
                        sawHandOwnership|=actor.Presentation.HandConstraintWeight>.7f;
                    }
                }

                Assert.That(motor.IsMantling,Is.False,"Airborne moving-platform mantle timed out.");
                Assert.That(motor.MantleLastRejection,Is.Null);
                Assert.That(sawRaise&&sawTransfer&&sawSettle,Is.True);
                Assert.That(sawHandOwnership,Is.True,"The real ledge catch never acquired hand IK ownership.");
                Assert.That(closestHand,Is.LessThan(.35f),"Neither visible hand reached the moving physical lip.");
                Assert.That(Vector3.Dot(ledge.transform.position-supportAtCatch,up),Is.GreaterThan(.08f),
                    "The proof platform did not move far enough during traversal to validate its local anchor.");
                yield return new WaitForSeconds(.8f);
                Assert.That(motor.HasStableSupport,Is.True,"The motor did not settle on the platform top.");
                Assert.That(motor.GroundSupport.HasSupport,Is.True);
                Assert.That(motor.GroundSupport.Candidate.SurfaceId,Is.EqualTo(moving.SurfaceId));
                Assert.That(actor.Presentation.CurrentAuthoredAction,Is.Not.EqualTo(EarthAuthoredActionId.Mantle));
                Debug.Log($"[AirborneMantleProof] closestHand={closestHand:F4}m " +
                          $"supportTravel={Vector3.Dot(ledge.transform.position-supportAtCatch,up):F4}m " +
                          $"sequence={motor.MantleSequence}.");
            }
            finally
            {
                input.Move=float2.zero;
                motor.ConfigureInputSource(actor.Input);
                Object.Destroy(input);
                Object.Destroy(ledge);
            }
        }
    }

    public sealed class AirborneMantleProofInput : MonoBehaviour,IPlanetMotorInputSource
    {
        public float2 Move;
        public bool JumpQueued;
        public PlanetMotorCommand SampleCommand(uint tick)
        {
            var command=new PlanetMotorCommand(tick,Move,JumpQueued);
            JumpQueued=false;
            return command;
        }
    }

    public sealed class AirborneMantleProofSurface : MonoBehaviour,IMovingSurface
    {
        private const uint Id=0x4D414E54u;
        private Rigidbody _body;
        private Vector3 _velocity;
        private Vector3 _up;
        public uint SurfaceId=>Id;
        public Vector3 SurfaceVelocity=>_velocity;
        public Vector3 SurfaceUp=>_up;
        public bool IsEmerging=>true;
        public SupportFrameSnapshot SupportFrame=>new(
            Id,1u,ToFloat3(transform.position),ToQuaternion(transform.rotation),
            ToFloat3(_velocity),float3.zero,ToFloat3(_velocity),ToFloat3(_up),true);
        public MovingSupportSnapshot Snapshot=>new(SupportFrame);

        public void Configure(Rigidbody body,Vector3 velocity,Vector3 up)
        {
            _body=body;
            _velocity=velocity;
            _up=up.normalized;
        }

        private void FixedUpdate()
        {
            if(_body!=null)_body.MovePosition(_body.position+_velocity*Time.fixedDeltaTime);
        }

        private static float3 ToFloat3(Vector3 value)=>new(value.x,value.y,value.z);
        private static quaternion ToQuaternion(Quaternion value)=>
            new(value.x,value.y,value.z,value.w);
    }
}
