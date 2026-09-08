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
            Vector3 direction = Vector3.forward;
            Vector3 lastPoint = default;
            bool found = false;
            // The central entrance is the unobstructed real arena descent lane.
            for (float z = 0f; z < 24f; z += .15f)
            {
                var ray = new Ray(new Vector3(-.26f, floor.bounds.max.y + 2f, z), -up);
                if (floor.Raycast(ray, out var hit, 20f))
                { lastPoint = hit.point; found = true; }
                else if (found) break;
            }
            Assert.That(found, Is.True, "Cannot locate the real arena entrance lip.");
            Vector3 start = lastPoint - direction * .6f;
            Assert.That(floor.Raycast(new Ray(start + up * 3f, -up), out var seat, 6f), Is.True);
            float bottom = motor.Capsule.height * .5f - motor.Capsule.center.y;
            Quaternion rotation = Quaternion.LookRotation(-direction, up);
            body.position = seat.point + up * (bottom + .035f);
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            motor.ResetAfterTeleport();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
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
            float minimumHeight = startHeight;
            int staleAirLocks = 0;
            var rows = new System.Text.StringBuilder("time,y,grounded,leftLock,rightLock,leftWeight,rightWeight,leftError,rightError\n");
            actor.Input.Move = new float2(0f, -.45f);
            for (int frame = 0; frame < 210; frame++)
            {
                yield return _frame;
                minimumHeight = Mathf.Min(minimumHeight, body.position.y);
                Assert.That(Vector3.Distance(lu.position, ll.position) + Vector3.Distance(ll.position, lf.position),
                    Is.EqualTo(leftLength).Within(.025f), "Left leg stretches during backward descent.");
                Assert.That(Vector3.Distance(ru.position, rl.position) + Vector3.Distance(rl.position, rf.position),
                    Is.EqualTo(rightLength).Within(.025f), "Right leg stretches during backward descent.");
                if (!motor.HasStableSupport && (feet.LeftFootLocked || feet.RightFootLocked)) staleAirLocks++;
                rows.AppendLine($"{Time.time:F4},{body.position.y:F4},{motor.HasStableSupport},{feet.LeftFootLocked},{feet.RightFootLocked},{feet.LeftFootIkWeight:F3},{feet.RightFootIkWeight:F3},{feet.LeftAnchorErrorMeters:F4},{feet.RightAnchorErrorMeters:F4}");
                _samples.Add(actor.Probe.Latest);
                if (frame == 30 || frame == 75 || frame == 125)
                {
                    Directory.CreateDirectory("BuildReports/SeptemberPremium");
                    ScreenCapture.CaptureScreenshot($"BuildReports/SeptemberPremium/BackwardEdge-{frame}.png");
                }
            }
            actor.Input.Move = float2.zero;
            Directory.CreateDirectory("BuildReports/SeptemberPremium");
            File.WriteAllText("BuildReports/SeptemberPremium/BackwardEdge.csv", rows.ToString());
            Assert.That(startHeight - minimumHeight, Is.GreaterThan(.2f), "Fixture never descended from the actual arena lip.");
            Assert.That(staleAirLocks, Is.LessThanOrEqualTo(2), "Airborne feet retain the old arena lock.");
            yield return new WaitForSeconds(.4f);
            Assert.That(motor.HasStableSupport, Is.True);
        }
    }
}
