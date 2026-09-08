using System.Collections;
using System.IO;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class SeptemberAnimationRescueRuntimeTests
    {
        [UnityTest]
        public IEnumerator BackwardArenaDescentReleasesOldFloorAndKeepsLegLengths()
        {
            Actor actor = _actors.Find(a => a.Presentation.GetComponent<EarthCharacterPoseController>() != null);
            var motor = actor.Presentation.GetComponentInParent<PlanetMotor>();
            var body = motor.Body;
            Collider floor = null;
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Collider>())
                    if (c.name == "Arena_FloorBase_INTACT") floor = c;
            Assert.That(floor, Is.Not.Null);
            Vector3 up = Vector3.up;
            // North is the entrance ARCH, not an unobstructed descent lane.
            // The central south edge opens directly onto the lower planet floor.
            Vector3 direction = Vector3.back;
            Vector3 lastPoint = default;
            bool found = false;
            Vector3 scale = motor.Capsule.transform.lossyScale;
            float radius = motor.Capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius * 2f, motor.Capsule.height * Mathf.Abs(scale.y));
            float bottom = height * .5f - motor.Capsule.center.y * scale.y;
            RaycastHit seat = default;
            // Validate an actual full-capsule corridor; do not silently accept a
            // visually open center point with shoulders/head inside architecture.
            foreach (float x in new[] { 0f, 1.25f, -1.25f, 2.5f, -2.5f })
            {
                bool touched = false;
                for (float z = 0f; z > floor.bounds.min.z - 1f; z -= .1f)
                {
                    var ray = new Ray(new Vector3(x, floor.bounds.max.y + 3f, z), -up);
                    if (floor.Raycast(ray, out var hit, 12f)) { lastPoint = hit.point; touched = true; }
                    else if (touched) break;
                }
                if (!touched) continue;
                Vector3 candidate = lastPoint - direction * 1.2f;
                if (!floor.Raycast(new Ray(candidate + up * 3f, -up), out seat, 6f)) continue;
                Vector3 foot = seat.point + up * .065f;
                bool blocked = false;
                foreach (RaycastHit obstruction in Physics.CapsuleCastAll(
                    foot + up * radius, foot + up * (height - radius), radius,
                    direction, 3.6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Collider obstacle = obstruction.collider;
                    if (obstacle == floor || obstacle.transform.IsChildOf(motor.transform)) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) { found = true; break; }
            }
            Assert.That(found, Is.True, "Cannot locate a clear full-capsule south descent corridor.");
            Quaternion rotation = Quaternion.LookRotation(-direction, up);
            Vector3 spawn = seat.point + up * (bottom + .065f);
            // Reset every puppet body/joint, not just its constrained root.
            // The production actor has a 1.2 scale; local capsule dimensions
            // alone previously embedded it 0.2 m into the floor.
            var puppet = motor.GetComponent<ActiveRagdollPuppet>();
            if (puppet != null) puppet.ResetPhysicalState(spawn, rotation);
            else
            {
                body.position = spawn;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            motor.ResetAfterTeleport();
            motor.ConfigureInputSource(actor.Input);
            motor.SetAimDirection(-direction);
            Physics.SyncTransforms();
            Assert.That(motor.enabled && motor.gameObject.activeInHierarchy, Is.True, "Motor controls are disabled.");
            Assert.That(body.isKinematic, Is.False, "Readiness/duel left the actor kinematic.");
            Assert.That(body.constraints & RigidbodyConstraints.FreezePosition, Is.EqualTo(RigidbodyConstraints.None));
            float settleUntil = Time.time + 1.5f;
            do { yield return new WaitForFixedUpdate(); }
            while (!motor.HasStableSupport && Time.time < settleUntil);
            Assert.That(motor.HasStableSupport, Is.True,
                $"Fixture did not acquire floor before descent: spawn={spawn}, body={body.position}, capsuleWorldHeight={height}.");
            Debug.Log($"[BackwardEdge] south lip={lastPoint}, seated={body.position}, world capsule={height:F3}/{radius:F3}, facing={motor.FacingForward}, input={actor.Input.enabled}.");
            yield return new WaitForSeconds(.2f);
            var animator = actor.Presentation.Animator;
            var feet = actor.Presentation.FootContactController;
            Transform lu = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform ll = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform ru = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rl = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float leftLength = Vector3.Distance(lu.position, ll.position) + Vector3.Distance(ll.position, lf.position);
            float rightLength = Vector3.Distance(ru.position, rl.position) + Vector3.Distance(rl.position, rf.position);
            float startHeight = body.position.y;
            Vector3 startPosition = body.position;
            float minimumHeight = startHeight;
            int staleAirLocks = 0;
            int inputSamples = 0;
            bool descentCaptured = false;
            float maximumProgress = 0f;
            var rows = new System.Text.StringBuilder("time,y,grounded,leftLock,rightLock,leftWeight,rightWeight,leftError,rightError,x,z,speed,commandY,progress\n");
            actor.Input.Move = new float2(0f, -.45f);
            float finishTime = Time.time + 4.5f;
            try
            {
                for (int frame = 0; frame < 900 && Time.time < finishTime; frame++)
                {
                    yield return _frame;
                    float progress = Vector3.Dot(body.position - startPosition, direction);
                    maximumProgress = Mathf.Max(maximumProgress, progress);
                    minimumHeight = Mathf.Min(minimumHeight, body.position.y);
                    if (motor.LastCommand.Move.y < -.4f) inputSamples++;
                    Assert.That(Vector3.Distance(lu.position, ll.position) + Vector3.Distance(ll.position, lf.position),
                        Is.EqualTo(leftLength).Within(.025f), "Left leg stretches during backward descent.");
                    Assert.That(Vector3.Distance(ru.position, rl.position) + Vector3.Distance(rl.position, rf.position),
                        Is.EqualTo(rightLength).Within(.025f), "Right leg stretches during backward descent.");
                    if (!motor.HasStableSupport && (feet.LeftFootLocked || feet.RightFootLocked)) staleAirLocks++;
                    rows.AppendLine($"{Time.time:F4},{body.position.y:F4},{motor.HasStableSupport},{feet.LeftFootLocked},{feet.RightFootLocked},{feet.LeftFootIkWeight:F3},{feet.RightFootIkWeight:F3},{feet.LeftAnchorErrorMeters:F4},{feet.RightAnchorErrorMeters:F4},{body.position.x:F4},{body.position.z:F4},{body.linearVelocity.magnitude:F4},{motor.LastCommand.Move.y:F3},{progress:F4}");
                    _samples.Add(actor.Probe.Latest);
                    if (frame == 30 || frame == 75 || frame == 125)
                    {
                        Directory.CreateDirectory("BuildReports/SeptemberPremium");
                        ScreenCapture.CaptureScreenshot($"BuildReports/SeptemberPremium/BackwardEdge-{frame}.png");
                    }
                    if (!descentCaptured && startHeight - body.position.y > .2f)
                    {
                        Directory.CreateDirectory("BuildReports/SeptemberPremium");
                        ScreenCapture.CaptureScreenshot("BuildReports/SeptemberPremium/BackwardEdge-Descent.png");
                        descentCaptured = true;
                    }
                    // Stop on the lower surface after physically clearing the lip;
                    // do not continue into the distant outer-ring arches.
                    if (progress > 3.2f) actor.Input.Move = float2.zero;
                }
            }
            finally
            {
                actor.Input.Move = float2.zero;
                Directory.CreateDirectory("BuildReports/SeptemberPremium");
                File.WriteAllText("BuildReports/SeptemberPremium/BackwardEdge.csv", rows.ToString());
            }
            Assert.That(inputSamples, Is.GreaterThan(5), "Motor never consumed the backward command.");
            Assert.That(maximumProgress, Is.GreaterThan(2f), "Fixture never physically crossed the arena edge.");
            Assert.That(startHeight - minimumHeight, Is.GreaterThan(.2f), "Fixture never descended from the actual arena lip.");
            Assert.That(staleAirLocks, Is.LessThanOrEqualTo(2), "Airborne feet retain the old arena lock.");
            yield return new WaitForSeconds(.4f);
            Assert.That(motor.HasStableSupport, Is.True);
        }
    }
}
