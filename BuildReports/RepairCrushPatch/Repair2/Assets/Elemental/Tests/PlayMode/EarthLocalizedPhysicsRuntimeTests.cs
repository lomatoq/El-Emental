using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthLocalizedPhysicsRuntimeTests
    {
        [UnityTest]
        public IEnumerator EveryRegionMovesThroughPhysXThenRecoversAndDeduplicates()
        {
            var actor = new GameObject("Local physical hit fixture");
            try
            {
                var bones = new Transform[EarthLocalizedPhysicsResponse.BoneCount];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject(((HumanoidRagdollBoneRole)i).ToString()).transform;
                    bones[i].SetParent(actor.transform, false);
                    bones[i].localPosition = new Vector3(i * .5f, 1f, 0f);
                }
                var physics = actor.AddComponent<HumanoidLocalizedPhysicsResponse>();
                physics.Configure(bones, actor.transform, null);
                yield return null;
                for (int i = 0; i < bones.Length; i++)
                {
                    physics.ResetToAnimation();
                    Vector3 start = bones[i].position;
                    var response = Hit((uint)i + 1u, start, EarthCharacterImpactResponse.Flinch);
                    Assert.That(physics.ApplyHit(in response, 1.2f), Is.True);
                    Assert.That(physics.ApplyHit(in response, 1.2f), Is.False, "The same response ID must not add a second impulse.");
                    Assert.That(physics.LastHitRegion, Is.EqualTo(i));
                    Assert.That(physics.ProxyBody(i).isKinematic, Is.False);
                    int parent = EarthLocalizedPhysicsResponse.Parent(i);
                    if (parent >= 0) Assert.That(physics.ProxyBody(parent).isKinematic, Is.False);
                    yield return new WaitForSeconds(.1f);
                    yield return new WaitForEndOfFrame();
                    Assert.That(physics.ProxyBody(i).position.z, Is.GreaterThan(start.z + .002f), $"Region {i}: no physical body displacement.");
                    Assert.That(bones[i].position.z, Is.GreaterThan(start.z + .002f), $"Region {i}: the rendered bone ignored the physics result.");
                    Assert.That(physics.CurrentMaximumAngle, Is.GreaterThan(.1f), $"Region {i}: no joint angular response.");
                    yield return new WaitForSeconds(.65f);
                    yield return new WaitForEndOfFrame();
                    Assert.That(physics.HasActiveResponse, Is.False);
                    Assert.That(physics.ProxyBody(i).isKinematic, Is.True);
                    Assert.That(Vector3.Distance(bones[i].position, start), Is.LessThan(.001f), $"Region {i}: pose did not recover.");
                }
                Assert.That(physics.AcceptedImpactCount, Is.EqualTo(bones.Length));
            }
            finally { Object.Destroy(actor); }
        }

        [UnityTest]
        public IEnumerator MediumStunExpiresWithoutDisablingTheMotorAndWeakHitsDoNotBlock()
        {
            var actor = new GameObject("Impact stun fixture");
            try
            {
                var body = actor.AddComponent<Rigidbody>();
                body.mass = 80f;
                body.useGravity = false;
                var motor = actor.AddComponent<PlanetMotor>();
                motor.enabled = false; // No gravity world is needed to verify the state gate.
                var target = actor.AddComponent<EarthCharacterImpactTarget>();
                target.Configure(EarthDuelFighterId.Player, 901u, body);
                int actionCancels = 0;
                motor.ImpactStunBegan += () => actionCancels++;
                Assert.That(target.ApplyStoneImpact(Vector3.zero, Vector3.forward, 40f, 10f,
                    EarthCharacterImpactSourceKind.LooseStone, 911u), Is.EqualTo(EarthCharacterImpactResponse.Flinch));
                Assert.That(motor.IsImpactStunned, Is.False);
                Assert.That(actionCancels, Is.Zero);
                Assert.That(target.ApplyStoneImpact(Vector3.zero, Vector3.forward, 40f, 20f,
                    EarthCharacterImpactSourceKind.LooseStone, 912u), Is.EqualTo(EarthCharacterImpactResponse.Stagger));
                Assert.That(motor.IsImpactStunned, Is.True);
                Assert.That(actionCancels, Is.EqualTo(1));
                Assert.That(motor.ImpactStunRemaining, Is.InRange(.23f, .241f));
                yield return new WaitForSeconds(.28f);
                Assert.That(motor.IsImpactStunned, Is.False);
                Assert.That(motor.enabled, Is.False, "The temporary gate must preserve the motor's prior enabled state.");
            }
            finally { Object.Destroy(actor); }
        }

        [UnityTest]
        public IEnumerator DestroyedBoneLifetimeStopsPoseWritesAndExplicitConfigureRebuilds()
        {
            var owner = new GameObject("Local response lifecycle owner");
            var skeleton = new GameObject("Disposable visible skeleton");
            try
            {
                var bones = new Transform[EarthLocalizedPhysicsResponse.BoneCount];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject($"Bone{i}").transform;
                    bones[i].SetParent(skeleton.transform, false);
                }
                var response = owner.AddComponent<HumanoidLocalizedPhysicsResponse>();
                response.Configure(bones, owner.transform, null);
                yield return null;
                Assert.That(response.IsReady, Is.True);
                Object.Destroy(skeleton);
                yield return null;
                yield return null;
                Assert.That(response.IsReady, Is.False, "Lost skeleton must invalidate the entire proxy lifetime.");
                skeleton = new GameObject("Replacement visible skeleton");
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject($"ReplacementBone{i}").transform;
                    bones[i].SetParent(skeleton.transform, false);
                }
                response.Configure(bones, owner.transform, null);
                yield return null;
                Assert.That(response.IsReady, Is.True);
                Assert.That(response.Bone(0), Is.SameAs(bones[0]));
                var hit = Hit(812u, bones[0].position, EarthCharacterImpactResponse.Flinch);
                Assert.That(response.ApplyHit(in hit, 1f), Is.True);
            }
            finally { Object.Destroy(owner); Object.Destroy(skeleton); }
        }

        private static EarthWorldResponseEvent Hit(uint id, Vector3 point, EarthCharacterImpactResponse response) => new(
            id, id, id + 100u, 1u, EarthWorldResponseKind.CharacterImpact, EarthCharacterImpactSourceKind.LooseStone,
            response, new float3(point.x, point.y, point.z), new float3(0, 0, -1), new float3(0, 0, 1), 80f, 50f, .3f);
    }
}
