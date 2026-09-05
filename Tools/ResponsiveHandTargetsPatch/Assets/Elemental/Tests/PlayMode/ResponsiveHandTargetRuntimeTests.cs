using System.Collections;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
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
        public IEnumerator GravityGripCarryKeepsHandsBodyRelativeWhileWalkingAndTurning()
        {
            Actor actor = _actors.Find(value => value.Presentation.PoseController != null);
            Assert.That(actor, Is.Not.Null);
            HumanoidCharacterPresentation presentation = actor.Presentation;
            PlanetMotor motor = presentation.GetComponentInParent<PlanetMotor>();
            MagicExecutor executor = GetPrivate<MagicExecutor>(presentation, "executor");
            Transform leftTarget = GetPrivate<Transform>(presentation, "leftHandTarget");
            Transform rightTarget = GetPrivate<Transform>(presentation, "rightHandTarget");
            EarthChoreographyDirector choreography = presentation.GetComponent<EarthChoreographyDirector>();
            Animator animator = presentation.Animator;
            Assert.That(motor, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(leftTarget, Is.Not.Null);
            Assert.That(rightTarget, Is.Not.Null);
            Assert.That(choreography, Is.Not.Null);

            Vector3 up = motor.LocalUp.normalized;
            Vector3 startForward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, startForward).normalized;
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            Assert.That(chest, Is.Not.Null);
            Vector3 focus = chest.position + startForward * 3f + up * 0.55f;
            GameObject[] stones = new GameObject[3];
            try
            {
                for (int index = 0; index < stones.Length; index++)
                {
                    GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stones[index] = stone;
                    stone.name = $"Responsive Gravity Carry Stone {index}";
                    SceneManager.MoveGameObjectToScene(stone, _scene);
                    stone.transform.position = focus + right * ((index - 1) * 0.34f);
                    stone.transform.localScale = Vector3.one * 0.28f;
                    Rigidbody body = stone.AddComponent<Rigidbody>();
                    body.useGravity = false;
                    body.mass = 9f + index;
                    stone.AddComponent<PhysicalImpactTarget>().Configure(body);
                }
                Physics.SyncTransforms();

                Assert.That(executor.TryBeginGravityWell(
                    stones[1].GetComponent<Collider>(), focus, up, true), Is.True);
                Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(2),
                    "The production area grip did not acquire a multi-stone carry fixture.");

                double contactDeadline = Time.realtimeSinceStartupAsDouble + 2d;
                while ((!presentation.HasResponsiveSustainedAim ||
                        presentation.HandConstraintWeight < .12f) &&
                       Time.realtimeSinceStartupAsDouble < contactDeadline)
                    yield return _frame;
                Assert.That(presentation.HasResponsiveSustainedAim, Is.True,
                    "The responsive target did not wait for and acquire sustained post-contact ownership.");

                Vector3 rootStart = motor.transform.position;
                Vector3 handCenterStart = (leftTarget.position + rightTarget.position) * .5f;
                float3 previousLocalAim = presentation.ResponsiveSustainedLocalAim;
                float maximumLocalAimStep = 0f;
                float maximumSymmetryError = 0f;
                float maximumChestYaw = 0f;
                float maximumResponsiveTorsoYaw = 0f;
                Vector3 requestedFacing = Quaternion.AngleAxis(55f, up) * startForward;
                motor.SetAimDirection(requestedFacing);
                actor.Input.Move = new float2(0.35f, 0.82f);

                const int movingFrames = 42;
                for (int frame = 0; frame < movingFrames; frame++)
                {
                    // The held cluster and the real motor both move. This is the
                    // production failure case where world-locked wrists lagged the body.
                    float progress = (frame + 1f) / movingFrames;
                    executor.UpdateGravityWell(focus + right * (0.65f * progress), up, requestedFacing);
                    yield return _frame;

                    float3 localAim = presentation.ResponsiveSustainedLocalAim;
                    float step = math.degrees(math.acos(math.clamp(
                        math.dot(previousLocalAim, localAim), -1f, 1f)));
                    maximumLocalAimStep = Mathf.Max(maximumLocalAimStep, step);
                    previousLocalAim = localAim;

                    Vector3 midpoint = (leftTarget.position + rightTarget.position) * .5f;
                    float leftRadius = Vector3.Distance(leftTarget.position, midpoint);
                    float rightRadius = Vector3.Distance(rightTarget.position, midpoint);
                    maximumSymmetryError = Mathf.Max(
                        maximumSymmetryError, Mathf.Abs(leftRadius - rightRadius));
                    float reach = Vector3.Distance(chest.position, midpoint);
                    Assert.That(reach, Is.InRange(
                        EarthResponsiveHandTargetSolver.MinimumReachMeters - .015f,
                        EarthResponsiveHandTargetSolver.MaximumReachMeters + .015f));
                    Assert.That(localAim.z, Is.GreaterThan(0f),
                        "The carry target crossed behind the torso reach cone.");
                    maximumChestYaw = Mathf.Max(
                        maximumChestYaw, Mathf.Abs(choreography.AppliedVisualPose.ChestEuler.y));
                    maximumResponsiveTorsoYaw = Mathf.Max(
                        maximumResponsiveTorsoYaw,
                        Mathf.Abs(EarthResponsiveHandTargetSolver.ResolveTorsoYawDegrees(
                            localAim, presentation.ResponsiveSustainedAimWeight)));

                    float allowedStep = EarthResponsiveHandTargetSolver.MaximumAimDegreesPerSecond *
                                        Mathf.Min(Time.deltaTime, .05f) + 1.5f;
                    Assert.That(step, Is.LessThanOrEqualTo(allowedStep),
                        "A moving Gravity Grip focus teleported the body-local hand direction.");
                }

                actor.Input.Move = float2.zero;
                for (int frame = 0; frame < 18; frame++) yield return _frame;

                Vector3 handCenterEnd = (leftTarget.position + rightTarget.position) * .5f;
                Vector3 finalForward = Vector3.ProjectOnPlane(motor.transform.forward, up).normalized;
                float rootTurn = Vector3.Angle(startForward, finalForward);
                float rootTravel = Vector3.Distance(rootStart, motor.transform.position);
                float handFrameTravel = Vector3.Distance(handCenterStart, handCenterEnd);
                float3 desired = EarthResponsiveHandTargetSolver.ConstrainAim(
                    ToFloat3(presentation.transform.InverseTransformDirection(
                        executor.GravityWellFocus - chest.position)));
                float remainingAimError = math.degrees(math.acos(math.clamp(
                    math.dot(desired, presentation.ResponsiveSustainedLocalAim), -1f, 1f)));

                Assert.That(rootTurn > 12f || rootTravel > .18f, Is.True,
                    "The production motor did not exercise a real carry turn or walk.");
                Assert.That(handFrameTravel, Is.GreaterThan(.08f),
                    "Both wrist targets remained effectively fixed in world space while the body moved.");
                Assert.That(maximumSymmetryError, Is.LessThan(.002f),
                    "The two-hand carry frame lost its symmetric held-space center.");
                Assert.That(maximumLocalAimStep, Is.GreaterThan(.1f),
                    "The hand frame did not react to the moving held focus.");
                Assert.That(remainingAimError, Is.LessThan(12f),
                    "The bounded response lagged the settled held focus for too long.");
                Assert.That(maximumChestYaw, Is.InRange(.25f,
                    EarthChoreographyVisualSolver.MaximumChestDegrees + .01f),
                    "The existing chest owner did not provide a gentle bounded carry response.");
                Assert.That(maximumResponsiveTorsoYaw, Is.InRange(.25f,
                    EarthResponsiveHandTargetSolver.MaximumTorsoYawDegrees + .01f),
                    "The moving carry never produced a bounded responsive chest aim contribution.");
            }
            finally
            {
                actor.Input.Move = float2.zero;
                executor.CancelGravityWell();
                for (int index = 0; index < stones.Length; index++)
                    if (stones[index] != null) Object.Destroy(stones[index]);
            }
        }

        private static T GetPrivate<T>(object owner, string fieldName) where T : class
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing runtime seam {owner.GetType().Name}.{fieldName}.");
            return field.GetValue(owner) as T;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
