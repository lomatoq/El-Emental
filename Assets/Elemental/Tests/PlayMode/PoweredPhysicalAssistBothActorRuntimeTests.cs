using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class PoweredPhysicalAssistBothActorRuntimeTests
    {
        private const string EarthCoreScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const float PosePositionTolerance = 0.0001f;
        private const float PoseRotationToleranceDegrees = 0.01f;

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo BridgeSourcesField =
            typeof(EarthPoweredPuppetPoseBridge).GetField("sourceBones", PrivateInstance);
        private static readonly FieldInfo BridgeTargetsField =
            typeof(EarthPoweredPuppetPoseBridge).GetField("poseTargets", PrivateInstance);
        private static readonly FieldInfo FootPuppetField =
            typeof(EarthFootContactController).GetField("poweredPuppet", PrivateInstance);
        private static readonly FieldInfo LeftSupportColliderField =
            typeof(EarthFootContactController).GetField("_leftSupportCollider", PrivateInstance);
        private static readonly FieldInfo RightSupportColliderField =
            typeof(EarthFootContactController).GetField("_rightSupportCollider", PrivateInstance);
        private static readonly FieldInfo ProceduralPuppetField =
            typeof(HumanoidProceduralBodyResponse).GetField("poweredPuppet", PrivateInstance);
        private static readonly FieldInfo SemanticProbeMaskField =
            typeof(ActiveRagdollPuppet).GetField("semanticProbeMask", PrivateInstance);
        private static readonly FieldInfo SemanticProbeRadiusField =
            typeof(ActiveRagdollPuppet).GetField("semanticProbeRadius", PrivateInstance);
        private static readonly MethodInfo PuppetFixedUpdateMethod =
            typeof(ActiveRagdollPuppet).GetMethod("FixedUpdate", PrivateInstance);

        [UnityTearDown]
        public IEnumerator EnsureEarthCoreFinishesUnloading()
        {
            yield return null;
            Scene scene = SceneManager.GetSceneByPath(EarthCoreScenePath);
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            DestroySceneRoots(scene);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator EarthCorePoweredPuppetsHonorBothActorOwnershipContracts()
        {
            Scene scene = default;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    EarthCoreScenePath,
                    LoadSceneMode.Additive);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                scene = SceneManager.GetSceneByPath(EarthCoreScenePath);
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                ActorHarness[] actors =
                {
                    ActorHarness.Create(scene, "Planet Character", 0xE201u),
                    ActorHarness.Create(scene, "Rumble Linebreaker Bot", 0xE202u)
                };

                int warmupTicks = 0;
                while (!AllRuntimeEvidenceReady(actors) && warmupTicks++ < 120)
                    yield return new WaitForFixedUpdate();

                Assert.That(AllRuntimeEvidenceReady(actors), Is.True,
                    "Both actors require stable ground, live foot probes and non-zero puppet gravity. " +
                    DescribeRuntimeEvidence(actors));

                int observerInstallFrame = Time.frameCount;
                var poseObservers = new PoweredPuppetPoseLateObserver[actors.Length];
                for (int index = 0; index < actors.Length; index++)
                {
                    var sources = (Transform[])BridgeSourcesField.GetValue(actors[index].Bridge);
                    var targets = (Transform[])BridgeTargetsField.GetValue(actors[index].Bridge);
                    poseObservers[index] = actors[index].Root.AddComponent<
                        PoweredPuppetPoseLateObserver>();
                    poseObservers[index].Configure(sources, targets);
                }

                // A null yield is batchmode-safe. The observer samples in
                // LateUpdate at order 1300, after the bridge importer order 1200,
                // so coroutine resumption order cannot create a false pass.
                int observerWaitFrames = 0;
                while (!AllObserversCapturedAfter(poseObservers, observerInstallFrame) &&
                       observerWaitFrames++ < 4)
                    yield return null;

                Assert.That(AllObserversCapturedAfter(poseObservers, observerInstallFrame),
                    Is.True,
                    "Both pose observers must capture a completed post-bridge LateUpdate.");
                for (int index = 0; index < actors.Length; index++)
                {
                    AssertPostWriterPoseEquality(actors[index], poseObservers[index]);
                    AssertPuppetOwnedCollidersAreExcluded(actors[index]);
                    AssertNonZeroRadialGravity(actors[index]);
                }

                for (int index = 0; index < actors.Length; index++)
                    AssertMovingSupportAndExactOnceDispatch(scene, actors[index]);
            }
            finally
            {
                DestroyAndUnloadScene(scene);
            }
        }

        private static bool AllObserversCapturedAfter(
            PoweredPuppetPoseLateObserver[] observers,
            int frame)
        {
            for (int index = 0; index < observers.Length; index++)
                if (observers[index] == null || observers[index].LastCapturedFrame <= frame)
                    return false;
            return true;
        }

        private static void AssertPostWriterPoseEquality(
            ActorHarness actor,
            PoweredPuppetPoseLateObserver observer)
        {
            Assert.That(BridgeSourcesField, Is.Not.Null);
            Assert.That(BridgeTargetsField, Is.Not.Null);
            Assert.That(actor.Bridge.enabled, Is.True, actor.Label);
            Assert.That(actor.Bridge.BindingsValid, Is.True, actor.Label);
            Assert.That(actor.Bridge.BindingCount, Is.EqualTo(8), actor.Label);

            var sources = (Transform[])BridgeSourcesField.GetValue(actor.Bridge);
            var targets = (Transform[])BridgeTargetsField.GetValue(actor.Bridge);
            Assert.That(sources, Is.Not.Null);
            Assert.That(targets, Is.Not.Null);
            Assert.That(sources.Length, Is.EqualTo(actor.Bridge.BindingCount));
            Assert.That(targets.Length, Is.EqualTo(actor.Bridge.BindingCount));
            Assert.That(observer.BindingCount, Is.EqualTo(actor.Bridge.BindingCount));
            Assert.That(observer.LastCapturedFrame, Is.GreaterThanOrEqualTo(0));
            for (int binding = 0; binding < observer.BindingCount; binding++)
            {
                Assert.That(observer.SourceAt(binding), Is.Not.Null,
                    $"{actor.Label}/source {binding}");
                Assert.That(observer.TargetAt(binding), Is.Not.Null,
                    $"{actor.Label}/target {binding}");
                Assert.That(ReferenceEquals(
                        observer.SourceAt(binding),
                        observer.TargetAt(binding)),
                    Is.False);
                Assert.That(observer.PositionErrorAt(binding),
                    Is.LessThan(PosePositionTolerance),
                    $"{actor.Label}/binding {binding} differed after bridge LateUpdate " +
                    $"on frame {observer.LastCapturedFrame}.");
                Assert.That(observer.RotationErrorAt(binding),
                    Is.LessThan(PoseRotationToleranceDegrees),
                    $"{actor.Label}/binding {binding} rotation differed after bridge LateUpdate " +
                    $"on frame {observer.LastCapturedFrame}.");
            }
        }

        private static void AssertPuppetOwnedCollidersAreExcluded(ActorHarness actor)
        {
            Assert.That(FootPuppetField, Is.Not.Null);
            Assert.That(LeftSupportColliderField, Is.Not.Null);
            Assert.That(RightSupportColliderField, Is.Not.Null);
            Assert.That(FootPuppetField.GetValue(actor.Feet), Is.SameAs(actor.Puppet),
                $"{actor.Label} foot probes are not bound to their powered puppet owner.");

            var owned = new Collider[16];
            int ownedCount = actor.Puppet.CopySelfCollidersNonAlloc(owned);
            Assert.That(ownedCount, Is.EqualTo(9), actor.Label);
            for (int index = 0; index < ownedCount; index++)
            {
                Assert.That(owned[index], Is.Not.Null, $"{actor.Label}/owned collider {index}");
                Assert.That(actor.Puppet.OwnsCollider(owned[index]), Is.True,
                    $"{actor.Label}/owned collider {index}");
            }

            var leftSupport = (Collider)LeftSupportColliderField.GetValue(actor.Feet);
            var rightSupport = (Collider)RightSupportColliderField.GetValue(actor.Feet);
            Assert.That(leftSupport, Is.Not.Null, $"{actor.Label}/left support");
            Assert.That(rightSupport, Is.Not.Null, $"{actor.Label}/right support");
            Assert.That(actor.Puppet.OwnsCollider(leftSupport), Is.False,
                $"{actor.Label} left foot selected a puppet-owned collider.");
            Assert.That(actor.Puppet.OwnsCollider(rightSupport), Is.False,
                $"{actor.Label} right foot selected a puppet-owned collider.");
        }

        private static void AssertNonZeroRadialGravity(ActorHarness actor)
        {
            AssertRadialAcceleration(
                actor.Label + "/motor",
                actor.Motor.GravityAcceleration,
                actor.Body.worldCenterOfMass,
                actor.PlanetCenter);

            Assert.That(actor.Joints.Length, Is.EqualTo(8), actor.Label);
            for (int index = 0; index < actor.Joints.Length; index++)
            {
                ActiveRagdollJoint joint = actor.Joints[index];
                Assert.That(joint, Is.Not.Null, $"{actor.Label}/joint {index}");
                Assert.That(joint.Body, Is.Not.Null, $"{actor.Label}/{joint.name}");
                GravityBody gravityBody = joint.Body.GetComponent<GravityBody>();
                Assert.That(gravityBody, Is.Not.Null,
                    $"{actor.Label}/{joint.name} lacks radial gravity.");
                AssertRadialAcceleration(
                    actor.Label + "/" + joint.name,
                    gravityBody.LastAcceleration,
                    joint.Body.worldCenterOfMass,
                    actor.PlanetCenter);
            }
        }

        private static void AssertMovingSupportAndExactOnceDispatch(
            Scene scene,
            ActorHarness actor)
        {
            Assert.That(PuppetFixedUpdateMethod, Is.Not.Null);
            Assert.That(ProceduralPuppetField, Is.Not.Null);
            Assert.That(SemanticProbeMaskField, Is.Not.Null);
            Assert.That(SemanticProbeRadiusField, Is.Not.Null);
            Assert.That(actor.Puppet.UsePoweredPhysicalAssist, Is.True, actor.Label);
            Assert.That(actor.Puppet.PoweredAssistConfigurationValid, Is.True, actor.Label);
            Assert.That(ProceduralPuppetField.GetValue(actor.Procedural),
                Is.SameAs(actor.Puppet),
                $"{actor.Label} action adapter is not bound to its powered puppet.");

            actor.Puppet.ResetPhysicalState(actor.Body.position, actor.Body.rotation);
            Assert.That(actor.Puppet.CanonicalMode,
                Is.EqualTo(CharacterPhysicalMode.AnimatedMotor), actor.Label);

            Vector3 up = actor.Motor.LocalUp.sqrMagnitude > 0.5f
                ? actor.Motor.LocalUp.normalized
                : (actor.Body.worldCenterOfMass - actor.PlanetCenter).normalized;
            Vector3 supportVelocity = -up * 4f;
            var support = new SupportFrameSnapshot(
                actor.ResponseId,
                1u,
                ToFloat3(actor.Body.position),
                quaternion.identity,
                ToFloat3(supportVelocity),
                float3.zero,
                ToFloat3(supportVelocity),
                ToFloat3(up),
                false);
            actor.Motor.ApplyMovingSupport(
                in support,
                actor.Motor.SupportFeetPoint(up),
                20f,
                100f);
            actor.Body.linearVelocity = supportVelocity;
            Assert.That(actor.Motor.CurrentSupportFrame.IsValid, Is.True, actor.Label);
            Assert.That(Vector3.Distance(
                    ToVector3(actor.Motor.CurrentSupportFrame.ContactPointVelocity),
                    actor.Body.linearVelocity),
                Is.LessThan(0.0001f),
                $"{actor.Label} fixture failed to establish zero support-relative velocity.");

            Collider fallSurface = CreateReachableFallSurface(scene, actor, up);
            AssertFallSurfaceIsInsideProductionProbe(actor, fallSurface, up);

            int observedActions = 0;
            EarthPhysicalActionRequest observedRequest = default;
            void ObserveAction(EarthPhysicalActionRequest request)
            {
                observedActions++;
                observedRequest = request;
            }

            int requestsBefore = actor.Puppet.PoweredActionRequestCount;
            uint acceptedBefore = actor.Presentation.AcceptedPhysicalActionCount;
            actor.Puppet.PhysicalActionRequested += ObserveAction;
            try
            {
                actor.Puppet.SetPoweredFootContactState(true, true);
                EarthWorldResponseEvent response = Response(
                    actor.ResponseId,
                    actor.Body.worldCenterOfMass,
                    up,
                    0.85f);
                EarthPoweredImpactDecision accepted =
                    actor.Puppet.ReceiveAcceptedWorldResponse(in response);
                EarthPoweredImpactDecision duplicate =
                    actor.Puppet.ReceiveAcceptedWorldResponse(in response);

                Assert.That(accepted.Accepted, Is.True, actor.Label);
                Assert.That(accepted.Owner,
                    Is.EqualTo(EarthPoweredImpactOwner.PoweredPhysicalAssist), actor.Label);
                Assert.That(accepted.EmitsImpulse || accepted.RequestsRagdoll,
                    Is.False, actor.Label);
                Assert.That(duplicate.Duplicate, Is.True, actor.Label);

                // Force the production FixedUpdate seam synchronously so no later
                // presentation LateUpdate can replace the deliberate no-contact sample.
                actor.Puppet.SetPoweredFootContactState(false, false);
                PuppetFixedUpdateMethod.Invoke(actor.Puppet, null);
                actor.Puppet.SetPoweredFootContactState(false, false);
                PuppetFixedUpdateMethod.Invoke(actor.Puppet, null);
            }
            finally
            {
                actor.Puppet.PhysicalActionRequested -= ObserveAction;
            }

            EarthPoweredAssistOutput output = actor.Puppet.LastPoweredAssistOutput;
            Assert.That((output.Behaviours & EarthPoweredBehaviour.FallArrest) == 0,
                Is.True,
                $"{actor.Label} interpreted support-frame velocity as actor fall velocity.");
            Assert.That(observedRequest.Kind, Is.Not.EqualTo(EarthPhysicalActionKind.FallArrest),
                actor.Label);
            Assert.That(observedRequest.ResponseId, Is.EqualTo(actor.ResponseId), actor.Label);
            Assert.That(observedActions, Is.EqualTo(1), actor.Label);
            Assert.That(actor.Puppet.PoweredActionRequestCount,
                Is.EqualTo(requestsBefore + 1), actor.Label);
            Assert.That(actor.Presentation.AcceptedPhysicalActionCount,
                Is.EqualTo(acceptedBefore + 1u), actor.Label);
            Assert.That(actor.Puppet.CanonicalMode,
                Is.EqualTo(CharacterPhysicalMode.Stagger), actor.Label);
        }

        private static Collider CreateReachableFallSurface(
            Scene scene,
            ActorHarness actor,
            Vector3 up)
        {
            var mask = (LayerMask)SemanticProbeMaskField.GetValue(actor.Puppet);
            Assert.That(mask.value, Is.Not.EqualTo(0), actor.Label);
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = actor.Label + " Moving-Support Fall Probe";
            SceneManager.MoveGameObjectToScene(surface, scene);
            surface.layer = FirstLayerInMask(mask.value);
            surface.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
            surface.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
            surface.transform.position = actor.Head.position - up * 0.9f;
            UnityEngine.Physics.SyncTransforms();
            return surface.GetComponent<Collider>();
        }

        private static void AssertFallSurfaceIsInsideProductionProbe(
            ActorHarness actor,
            Collider expectedSurface,
            Vector3 up)
        {
            var mask = (LayerMask)SemanticProbeMaskField.GetValue(actor.Puppet);
            float radius = (float)SemanticProbeRadiusField.GetValue(actor.Puppet);
            var hits = new RaycastHit[32];
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                actor.Head.position,
                radius,
                -up,
                hits,
                EarthPoweredPhysicalAssist.MaximumSemanticReach,
                mask,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int index = 0; index < hitCount && index < hits.Length; index++)
            {
                if (hits[index].collider == expectedSurface)
                {
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True,
                $"{actor.Label} fall surface was outside the production semantic probe.");
        }

        private static void AssertRadialAcceleration(
            string label,
            Vector3 acceleration,
            Vector3 worldPosition,
            Vector3 planetCenter)
        {
            Assert.That(IsFinite(acceleration), Is.True, label);
            Assert.That(acceleration.magnitude, Is.GreaterThan(0.1f), label);
            Vector3 radialOut = (worldPosition - planetCenter).normalized;
            Assert.That(Vector3.Dot(acceleration.normalized, radialOut),
                Is.LessThan(-0.95f), label);
        }

        private static bool AllRuntimeEvidenceReady(ActorHarness[] actors)
        {
            for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
            {
                ActorHarness actor = actors[actorIndex];
                if (!actor.Motor.HasStableSupport ||
                    !actor.Feet.LeftHasContact ||
                    !actor.Feet.RightHasContact ||
                    actor.Motor.GravityAcceleration.sqrMagnitude <= 0.01f)
                    return false;
                for (int jointIndex = 0; jointIndex < actor.Joints.Length; jointIndex++)
                {
                    Rigidbody body = actor.Joints[jointIndex].Body;
                    GravityBody gravity = body != null ? body.GetComponent<GravityBody>() : null;
                    if (gravity == null || gravity.LastAcceleration.sqrMagnitude <= 0.01f)
                        return false;
                }
            }
            return true;
        }

        private static string DescribeRuntimeEvidence(ActorHarness[] actors)
        {
            var parts = new List<string>(actors.Length);
            for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
            {
                ActorHarness actor = actors[actorIndex];
                int jointsWithoutGravity = 0;
                for (int jointIndex = 0; jointIndex < actor.Joints.Length; jointIndex++)
                {
                    Rigidbody body = actor.Joints[jointIndex].Body;
                    GravityBody gravity = body != null ? body.GetComponent<GravityBody>() : null;
                    if (gravity == null || gravity.LastAcceleration.sqrMagnitude <= 0.01f)
                        jointsWithoutGravity++;
                }
                parts.Add(
                    $"{actor.Label}[pos={actor.Body.position:F3}, " +
                    $"stable={actor.Motor.HasStableSupport}, " +
                    $"feet={actor.Feet.LeftHasContact}/{actor.Feet.RightHasContact}, " +
                    $"motorGravity={actor.Motor.GravityAcceleration.magnitude:F3}, " +
                    $"joints={actor.Joints.Length}, jointsWithoutGravity={jointsWithoutGravity}, " +
                    $"supportHits={DescribeSupportHits(actor)}]");
            }
            return string.Join("; ", parts);
        }

        private static string DescribeSupportHits(ActorHarness actor)
        {
            Vector3 up = actor.Motor.LocalUp.sqrMagnitude > 0.5f
                ? actor.Motor.LocalUp.normalized
                : (actor.Body.worldCenterOfMass - actor.PlanetCenter).normalized;
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(
                actor.Body.worldCenterOfMass + up * 0.25f,
                -up,
                4f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var values = new List<string>(Mathf.Min(6, hits.Length));
            for (int index = 0; index < hits.Length && values.Count < 6; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null || actor.Puppet.OwnsCollider(collider)) continue;
                values.Add(
                    $"{collider.name}@{hits[index].distance:F3}/" +
                    $"n{Vector3.Dot(hits[index].normal, up):F2}");
            }
            return values.Count > 0 ? string.Join(",", values) : "none";
        }

        private static EarthWorldResponseEvent Response(
            uint responseId,
            Vector3 point,
            Vector3 up,
            float intensity) => new EarthWorldResponseEvent(
            responseId,
            responseId + 100u,
            responseId + 200u,
            1u,
            EarthWorldResponseKind.CharacterImpact,
            EarthCharacterImpactSourceKind.Physics,
            EarthCharacterImpactResponse.Stagger,
            ToFloat3(point),
            ToFloat3(up),
            new float3(1f, 0f, 0f),
            10f,
            50f,
            intensity);

        private static int FirstLayerInMask(int mask)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask & (1 << layer)) != 0) return layer;
            }
            Assert.Fail("The semantic probe mask contains no layer.");
            return 0;
        }

        private static ActiveRagdollJoint[] FindOwnedJoints(
            Scene scene,
            ActiveRagdollPuppet puppet)
        {
            var owned = new List<ActiveRagdollJoint>();
            ActiveRagdollJoint[] candidates = FindInSceneAll<ActiveRagdollJoint>(scene);
            for (int index = 0; index < candidates.Length; index++)
            {
                ActiveRagdollJoint candidate = candidates[index];
                if (candidate.TargetPose != null &&
                    candidate.TargetPose.IsChildOf(puppet.transform))
                    owned.Add(candidate);
            }
            return owned.ToArray();
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name) return roots[index];
            }
            return null;
        }

        private static T[] FindInSceneAll<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                found.AddRange(roots[index].GetComponentsInChildren<T>(true));
            return found.ToArray();
        }

        private static void DestroyAndUnloadScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            DestroySceneRoots(scene);
            SceneManager.UnloadSceneAsync(scene);
        }

        private static void DestroySceneRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                Object.DestroyImmediate(roots[index]);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(float3 value) =>
            new Vector3(value.x, value.y, value.z);

        private sealed class ActorHarness
        {
            private ActorHarness(
                string label,
                uint responseId,
                GameObject root,
                ActiveRagdollPuppet puppet,
                ActiveRagdollJoint[] joints,
                PlanetMotor motor,
                Rigidbody body,
                EarthPoweredPuppetPoseBridge bridge,
                EarthFootContactController feet,
                HumanoidProceduralBodyResponse procedural,
                HumanoidCharacterPresentation presentation,
                Transform head,
                Vector3 planetCenter)
            {
                Label = label;
                ResponseId = responseId;
                Root = root;
                Puppet = puppet;
                Joints = joints;
                Motor = motor;
                Body = body;
                Bridge = bridge;
                Feet = feet;
                Procedural = procedural;
                Presentation = presentation;
                Head = head;
                PlanetCenter = planetCenter;
            }

            public string Label { get; }
            public uint ResponseId { get; }
            public GameObject Root { get; }
            public ActiveRagdollPuppet Puppet { get; }
            public ActiveRagdollJoint[] Joints { get; }
            public PlanetMotor Motor { get; }
            public Rigidbody Body { get; }
            public EarthPoweredPuppetPoseBridge Bridge { get; }
            public EarthFootContactController Feet { get; }
            public HumanoidProceduralBodyResponse Procedural { get; }
            public HumanoidCharacterPresentation Presentation { get; }
            public Transform Head { get; }
            public Vector3 PlanetCenter { get; }

            public static ActorHarness Create(Scene scene, string rootName, uint responseId)
            {
                GameObject root = FindRootByName(scene, rootName);
                Assert.That(root, Is.Not.Null, rootName);
                ActiveRagdollPuppet puppet = root.GetComponent<ActiveRagdollPuppet>();
                PlanetMotor motor = root.GetComponent<PlanetMotor>();
                Rigidbody body = root.GetComponent<Rigidbody>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                EarthPoweredPuppetPoseBridge bridge =
                    root.GetComponentInChildren<EarthPoweredPuppetPoseBridge>(true);
                EarthFootContactController feet =
                    root.GetComponentInChildren<EarthFootContactController>(true);
                HumanoidProceduralBodyResponse procedural =
                    root.GetComponentInChildren<HumanoidProceduralBodyResponse>(true);
                HumanoidCharacterPresentation presentation =
                    root.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                VoxelPlanetBehaviour[] planets = FindInSceneAll<VoxelPlanetBehaviour>(scene);
                Assert.That(planets.Length, Is.GreaterThan(0), rootName);
                VoxelPlanetBehaviour planet = planets[0];

                Assert.That(puppet, Is.Not.Null, rootName);
                Assert.That(motor, Is.Not.Null, rootName);
                Assert.That(body, Is.Not.Null, rootName);
                Assert.That(animator, Is.Not.Null, rootName);
                Assert.That(bridge, Is.Not.Null, rootName);
                Assert.That(feet, Is.Not.Null, rootName);
                Assert.That(procedural, Is.Not.Null, rootName);
                Assert.That(presentation, Is.Not.Null, rootName);
                Assert.That(planet, Is.Not.Null, rootName);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Assert.That(head, Is.Not.Null, rootName);

                return new ActorHarness(
                    rootName,
                    responseId,
                    root,
                    puppet,
                    FindOwnedJoints(scene, puppet),
                    motor,
                    body,
                    bridge,
                    feet,
                    procedural,
                    presentation,
                    head,
                    planet.transform.position);
            }
        }
    }
}
