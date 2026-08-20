using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthMagicExpansionRuntimeTests
    {
        private sealed class PlatformMotorInputSource : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public bool JumpQueued;

            public PlanetMotorCommand SampleCommand(uint tick)
            {
                PlanetMotorCommand command = new PlanetMotorCommand(tick, Move, JumpQueued);
                JumpQueued = false;
                return command;
            }
        }

        [UnityTest]
        public IEnumerator MmbSessionKeepsFracturedWallClusterLatchedUntilRelease()
        {
            GameObject root = new GameObject("MMB Fracture Session Runtime");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            EarthWallProfile wallProfile = ScriptableObject.CreateInstance<EarthWallProfile>();
            EarthGravityWellProfile gravityProfile = ScriptableObject.CreateInstance<EarthGravityWellProfile>();
            pool.Configure(1, null, null, wallProfile);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            executor.ConfigureEarthExtensions(null, null, gravityProfile);
            root.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-4f, 24f, 0f),
                new Vector3(4f, 24f, 0f),
                Vector3.zero,
                4f,
                0.65f);
            yield return new WaitForSeconds(0.7f);

            Assert.That(executor.TryBeginGravityWell(
                wall.GetComponent<Collider>(),
                wall.transform.position + wall.transform.up * 2f,
                wall.transform.up), Is.True);
            yield return new WaitForSeconds(1.2f);
            Assert.That(wall.IsCollapsing, Is.True);
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(20));
            int activeBefore = wall.ActiveFracturePieceCount;
            yield return new WaitForSeconds(3f);
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(activeBefore),
                "Latched fracture targets must not shrink or return to the pool while MMB is held.");
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(20));

            executor.CancelGravityWell();
            Assert.That(executor.GravityWellCapturedCount, Is.Zero);
            yield return new WaitForFixedUpdate();
            EarthWallPiece releasedPiece = wall.FirstFracturePiece != null
                ? wall.FirstFracturePiece.GetComponent<EarthWallPiece>()
                : null;
            Assert.That(releasedPiece, Is.Not.Null);
            Assert.That(releasedPiece.Body.isKinematic, Is.False);
            Assert.That(releasedPiece.Body.detectCollisions, Is.True);

            Object.Destroy(root);
            Object.Destroy(wallProfile);
            Object.Destroy(gravityProfile);
        }

        [UnityTest]
        public IEnumerator MmbPressLocksOneExplicitTargetAndNeverSweepsNearbyBodies()
        {
            GameObject executorObject = new GameObject("Fixed MMB Target Runtime");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            EarthGravityWellProfile gravityProfile = ScriptableObject.CreateInstance<EarthGravityWellProfile>();
            executor.ConfigureEarthExtensions(null, null, gravityProfile);

            GameObject aimedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            aimedObject.name = "Explicit MMB Target";
            Rigidbody aimedBody = aimedObject.AddComponent<Rigidbody>();
            aimedBody.useGravity = false;
            PhysicalImpactTarget aimed = aimedObject.AddComponent<PhysicalImpactTarget>();
            aimed.Configure(aimedBody);
            GameObject neighbourObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neighbourObject.name = "Nearby Unselected Earth Target";
            neighbourObject.transform.position = Vector3.right * 0.35f;
            Rigidbody neighbourBody = neighbourObject.AddComponent<Rigidbody>();
            neighbourBody.useGravity = false;
            PhysicalImpactTarget neighbour = neighbourObject.AddComponent<PhysicalImpactTarget>();
            neighbour.Configure(neighbourBody);

            Assert.That(executor.TryBeginGravityWell(
                aimedObject.GetComponent<Collider>(), Vector3.up * 1.2f, Vector3.up), Is.True);
            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
            Assert.That(executor.GravityWellCapturedCount, Is.EqualTo(1),
                "An MMB session may only grow from the selected structure's fracture event, never an overlap sweep.");

            executor.CancelGravityWell();
            Object.Destroy(executorObject);
            Object.Destroy(aimedObject);
            Object.Destroy(neighbourObject);
            Object.Destroy(gravityProfile);
        }

        [UnityTest]
        public IEnumerator MmbRmbTapAndChargeProduceDistinctPhysicalLaunches()
        {
            GameObject caster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            caster.name = "MMB Throw Caster";
            Rigidbody casterBody = caster.AddComponent<Rigidbody>();
            casterBody.useGravity = false;
            casterBody.isKinematic = true;
            MagicExecutor executor = caster.AddComponent<MagicExecutor>();
            EarthGravityWellProfile profile = ScriptableObject.CreateInstance<EarthGravityWellProfile>();
            executor.ConfigureEarthExtensions(null, null, profile);

            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.name = "MMB Throw Stone";
            stone.transform.position = Vector3.forward * 2f;
            Rigidbody body = stone.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 18f;
            PhysicalImpactTarget target = stone.AddComponent<PhysicalImpactTarget>();
            target.Configure(body);

            Assert.That(executor.TryBeginGravityWell(
                stone.GetComponent<Collider>(), stone.transform.position, Vector3.up), Is.True);
            Assert.That(executor.BeginGravityClusterThrow(Vector3.forward), Is.True);
            int directCount = executor.ReleaseGravityClusterThrow(Vector3.forward);
            float directSpeed = body.linearVelocity.magnitude;
            Assert.That(directCount, Is.EqualTo(1));
            Assert.That(directSpeed, Is.InRange(14f, 16f));
            Assert.That(Vector3.Dot(body.linearVelocity.normalized, Vector3.forward), Is.GreaterThan(0.96f));
            Assert.That(stone.GetComponent<EarthLaunchCollisionGrace>(), Is.Not.Null);

            body.position = Vector3.forward * 2f;
            body.linearVelocity = Vector3.zero;
            Assert.That(executor.TryBeginGravityWell(
                stone.GetComponent<Collider>(), stone.transform.position, Vector3.up), Is.True);
            Assert.That(executor.BeginGravityClusterThrow(Vector3.forward), Is.True);
            yield return new WaitForSecondsRealtime(0.48f);
            executor.UpdateGravityClusterThrow(Vector3.forward);
            Assert.That(executor.GravityClusterThrowCharge01, Is.GreaterThan(0.5f));
            int chargedCount = executor.ReleaseGravityClusterThrow(Vector3.forward);
            Assert.That(chargedCount, Is.EqualTo(1));
            Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(directSpeed + 5f));
            Assert.That(executor.IsGravityWellActive, Is.False);
            Assert.That(casterBody.linearVelocity, Is.EqualTo(Vector3.zero),
                "Scoped launch grace and the throw solver may never recoil the caster.");

            Object.Destroy(caster);
            Object.Destroy(stone);
            Object.Destroy(profile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExtractedRocksUseBeveledMeshAndConvexCollider()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthFragmentPool pool = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                pool = root.GetComponentInChildren<EarthFragmentPool>(true);
                if (pool != null) break;
            }
            Assert.That(pool, Is.Not.Null);
            EarthFragment fragment = pool.Acquire(null, new Vector3(0f, 30f, 0f), 0.8f, 100f);
            MeshFilter filter = fragment.GetComponent<MeshFilter>();
            MeshCollider collider = fragment.GetComponent<MeshCollider>();

            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThanOrEqualTo(72));
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.convex, Is.True);
            Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator VectorFieldAcceleratesEveryPhysicsTickAndReleaseAddsImpulseWithoutTeleporting()
        {
            GameObject executorObject = new GameObject("Vector Field Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(0f, 0f, 4f);
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 120f;
            Physics.SyncTransforms();

            Assert.That(executor.TryBeginVectorField(
                target.GetComponent<Collider>(), body, body.worldCenterOfMass, Vector3.forward), Is.True);
            float previousSpeed = 0f;
            for (int tick = 0; tick < 18; tick++)
            {
                executor.UpdateVectorField(Vector3.forward, 1f);
                yield return new WaitForFixedUpdate();
                float speed = Vector3.Dot(body.linearVelocity, Vector3.forward);
                Assert.That(speed + 0.001f, Is.GreaterThanOrEqualTo(previousSpeed));
                previousSpeed = speed;
            }

            Vector3 beforeRelease = body.position;
            float speedBeforeRelease = body.linearVelocity.z;
            Assert.That(executor.ReleaseVectorField(), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity.z, Is.GreaterThan(speedBeforeRelease));
            Assert.That(Vector3.Distance(body.position, beforeRelease), Is.LessThan(1.2f));
            Assert.That(executor.IsVectorFieldActive, Is.False);

            Object.Destroy(executorObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ChargedFieldSlidesWallSeveralMetersAndKeepsItUpright()
        {
            GameObject root = new GameObject("Vector Wall Runtime");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(1, null, null);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            root.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3f,
                0.55f);
            yield return new WaitForSeconds(0.75f);
            Vector3 initialPosition = wall.transform.position;
            Assert.That(executor.TryBeginVectorField(
                wall.GetComponent<Collider>(), wall.Body, wall.transform.position, Vector3.forward), Is.True);
            for (int tick = 0; tick < 75; tick++)
            {
                executor.UpdateVectorField(Vector3.forward, 1f);
                yield return new WaitForFixedUpdate();
            }
            executor.ReleaseVectorField();
            yield return new WaitForFixedUpdate();

            float distance = Vector3.Distance(initialPosition, wall.transform.position);
            Vector3 localUp = wall.transform.position.normalized;
            Assert.That(distance, Is.GreaterThan(2f));
            Assert.That(Vector3.Angle(localUp, wall.transform.up), Is.LessThan(1f));
            Assert.That(wall.Body.linearVelocity.magnitude, Is.LessThanOrEqualTo(14.1f));

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator QuickTapFieldImmediatelySlidesAWholeWallWithoutRecoil()
        {
            GameObject root = new GameObject("Quick Vector Wall Runtime");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(1, null, null);
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            root.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-3f, 24f, 0f),
                new Vector3(3f, 24f, 0f),
                Vector3.zero,
                3f,
                0.55f);
            yield return new WaitForSeconds(0.75f);
            Vector3 wallStart = wall.transform.position;

            Assert.That(executor.TryBeginVectorField(
                wall.GetComponent<Collider>(), wall.Body, wall.transform.position, Vector3.forward), Is.True);
            executor.UpdateVectorField(Vector3.forward, 0f);
            Assert.That(executor.ReleaseVectorField(), Is.True);
            yield return new WaitForFixedUpdate();
            float releaseSpeed = Vector3.Dot(wall.Body.linearVelocity, Vector3.forward);
            for (int tick = 0; tick < 11; tick++) yield return new WaitForFixedUpdate();

            Assert.That(releaseSpeed, Is.GreaterThan(4.5f),
                "A tap must feel like an immediate directed shove, not a failed charge.");
            Assert.That(Vector3.Distance(wall.transform.position, wallStart), Is.GreaterThan(0.8f));
            Assert.That(Vector3.Angle(wall.transform.up, wall.transform.position.normalized), Is.LessThan(1f));
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator GrabbedWallPieceBreaksItsBondsAndPausesShrinkLifecycle()
        {
            GameObject root = new GameObject("Grabbable Wall Debris");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(1, null, null);
            EarthTelekinesisController telekinesis = root.AddComponent<EarthTelekinesisController>();
            MagicExecutor executor = root.AddComponent<MagicExecutor>();
            executor.ConfigureTelekinesis(telekinesis);
            root.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-2f, 16f, 0f), new Vector3(2f, 16f, 0f),
                Vector3.zero, 2.5f, 0.55f);
            yield return new WaitForSeconds(0.6f);
            wall.ApplyRockImpact(wall.transform.position, Vector3.forward, 100f);
            yield return new WaitForFixedUpdate();
            EarthWallPiece piece = wall.FirstFracturePiece.GetComponent<EarthWallPiece>();
            Rigidbody body = piece.Body;
            int bondsBefore = wall.RemainingBondCount;
            BendTuning tuning = BendTuning.Default;
            Assert.That(executor.TryAcquireExistingEarthBody(
                body, body.worldCenterOfMass, in tuning, 1u, piece), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(wall.RemainingBondCount, Is.LessThan(bondsBefore));

            yield return new WaitForSeconds(4f);
            Assert.That(piece.gameObject.activeSelf, Is.True);
            Assert.That(piece.transform.localScale.sqrMagnitude, Is.GreaterThan(0.1f));
            executor.ReleaseHeldEarth(Vector3.forward, Vector3.zero, 0f, 2u, out _);

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator PlatformIsWalkableSolidUntilHeavyImpactThenUsesGrabbablePieces()
        {
            EarthPlatformProfile profile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            GameObject root = new GameObject("Platform Pool Runtime");
            root.SetActive(false);
            EarthPlatformPool pool = root.AddComponent<EarthPlatformPool>();
            pool.Configure(2, null, profile);
            root.SetActive(true);
            var path = new List<float3>
            {
                new float3(-2f, 24f, -2f), new float3(2f, 24f, -2f),
                new float3(2f, 24f, 2f), new float3(-2f, 24f, 2f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, float3.zero);
            EarthPlatform platform = pool.Acquire(in geometry, 1.4f, 0.2f);
            yield return null;

            MeshCollider solid = platform.GetComponent<MeshCollider>();
            yield return new WaitForSeconds(0.6f);
            Assert.That(solid.enabled, Is.True);
            Assert.That(solid.sharedMesh, Is.Not.Null);
            Mesh solidMesh = solid.sharedMesh;
            Vector3[] vertices = solidMesh.vertices;
            int[] triangles = solidMesh.triangles;
            Vector3 bottomNormal = Vector3.Cross(
                vertices[triangles[1]] - vertices[triangles[0]],
                vertices[triangles[2]] - vertices[triangles[0]]).normalized;
            int topTriangle = 3;
            Vector3 topNormal = Vector3.Cross(
                vertices[triangles[topTriangle + 1]] - vertices[triangles[topTriangle]],
                vertices[triangles[topTriangle + 2]] - vertices[triangles[topTriangle]]).normalized;
            Assert.That(bottomNormal.y, Is.LessThan(-0.9f), "Platform underside must face outward/down.");
            Assert.That(topNormal.y, Is.GreaterThan(0.9f), "Walkable platform cap must face outward/up.");
            Assert.That(platform.ApplyStructureImpact(platform.transform.position, Vector3.forward, 2200f), Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(platform.IsFractured, Is.True);
            Assert.That(solid.enabled, Is.False);
            Assert.That(platform.ActivePieceCount, Is.GreaterThan(5));
            EarthPlatformPiece piece = platform.FirstActivePiece;
            Assert.That(piece, Is.Not.Null);
            Assert.That(piece.IsEarthTargetValid, Is.True);

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator PlatformLightImpactDetachesLocalCellsWhileFoundationIslandStaysSupported()
        {
            EarthPlatformProfile profile = ScriptableObject.CreateInstance<EarthPlatformProfile>();
            GameObject root = new GameObject("Platform Structural Island Runtime");
            root.SetActive(false);
            EarthPlatformPool pool = root.AddComponent<EarthPlatformPool>();
            pool.Configure(1, null, profile);
            root.SetActive(true);
            var path = new List<float3>
            {
                new float3(-3f, 24f, -2.2f), new float3(3f, 24f, -2.2f),
                new float3(3.2f, 24f, 1.8f), new float3(-2.7f, 24f, 2.4f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, float3.zero);
            EarthPlatform platform = pool.Acquire(in geometry, 1.6f, 0.25f);
            for (int tick = 0; tick < 45; tick++) yield return new WaitForFixedUpdate();

            Assert.That(platform.ApplyStructureImpact(
                platform.SurfaceTopPoint + platform.transform.right * 0.65f,
                platform.transform.forward,
                profile.FractureImpulse * 1.08f), Is.True);
            yield return new WaitForFixedUpdate();
            var targets = new IEarthPhysicalTarget[48];
            int count = platform.CopyActiveTargetsNonAlloc(targets);
            int dynamicAfterLight = 0;
            int supportedAfterLight = 0;
            for (int index = 0; index < count; index++)
            {
                if (targets[index].Body.isKinematic) supportedAfterLight++;
                else dynamicAfterLight++;
            }
            Assert.That(dynamicAfterLight, Is.InRange(1, 10),
                "A light hit should chip a local cluster, not turn the complete platform into debris.");
            Assert.That(supportedAfterLight, Is.GreaterThan(12),
                "Foundation-connected cells must remain a standing structural island.");

            platform.ApplyStructureImpact(
                platform.SurfaceTopPoint,
                platform.transform.forward,
                profile.FractureImpulse * 4.6f);
            yield return new WaitForFixedUpdate();
            int dynamicAfterHeavy = 0;
            count = platform.CopyActiveTargetsNonAlloc(targets);
            for (int index = 0; index < count; index++)
                if (!targets[index].Body.isKinematic) dynamicAfterHeavy++;
            Assert.That(dynamicAfterHeavy, Is.GreaterThan(dynamicAfterLight + 5));

            Object.Destroy(root);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator PlatformDrawnUnderPlayerCarriesWithoutFractureOrRagdoll()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthPlatformPool pool = null;
            PlanetMotor motor = null;
            ActiveRagdollPuppet puppet = null;
            Collider planet = null;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                pool ??= rootObject.GetComponentInChildren<EarthPlatformPool>(true);
                motor ??= rootObject.GetComponentInChildren<PlanetMotor>(true);
                puppet ??= rootObject.GetComponentInChildren<ActiveRagdollPuppet>(true);
                if (rootObject.name == "Planet Collision Proxy") planet = rootObject.GetComponent<Collider>();
            }
            Assert.That(pool, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(planet, Is.Not.Null);
            Rigidbody rider = motor.GetComponent<Rigidbody>();
            Vector3 up = (rider.worldCenterOfMass - planet.transform.position).normalized;
            Vector3 surface = planet.ClosestPoint(rider.worldCenterOfMass);
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            var path = new List<float3>
            {
                ToFloat3(surface - right * 1.6f - forward * 1.6f),
                ToFloat3(surface + right * 1.6f - forward * 1.6f),
                ToFloat3(surface + right * 1.6f + forward * 1.6f),
                ToFloat3(surface - right * 1.6f + forward * 1.6f)
            };
            EarthPlatformGeometry geometry = EarthPlatformGeometrySolver.Build(path, ToFloat3(planet.transform.position));
            float initialRadius = Vector3.Distance(rider.worldCenterOfMass, planet.transform.position);
            EarthPlatform platform = pool.Acquire(in geometry, 1.4f, 0.24f);
            yield return new WaitForFixedUpdate();
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(platform.SurfaceId),
                "A platform committed below the player must register its rider on the first physics step.");
            CapsuleCollider riderCapsule = motor.GetComponent<CapsuleCollider>();
            float minimumFootClearance = float.PositiveInfinity;
            for (int tick = 0; tick < 65; tick++)
            {
                yield return new WaitForFixedUpdate();
                Vector3 scale = motor.transform.lossyScale;
                float radius = riderCapsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float halfHeight = Mathf.Max(radius, riderCapsule.height * 0.5f * Mathf.Abs(scale.y));
                Vector3 feet = motor.transform.TransformPoint(riderCapsule.center) - up * halfHeight;
                minimumFootClearance = Mathf.Min(minimumFootClearance,
                    Vector3.Dot(feet - platform.SurfaceTopPoint, up));
            }
            float liftedRadius = Vector3.Distance(rider.worldCenterOfMass, planet.transform.position);

            Assert.That(platform.IsFractured, Is.False);
            Assert.That(liftedRadius, Is.GreaterThan(initialRadius + 0.35f),
                $"initial={initialRadius:F3}, lifted={liftedRadius:F3}, " +
                $"topRadius={Vector3.Distance(platform.SurfaceTopPoint, planet.transform.position):F3}, " +
                $"emergence={platform.Emergence01:F3}, clearance={minimumFootClearance:F3}, " +
                $"movingSurface={motor.MovingSurfaceId}, velocity={rider.linearVelocity}.");
            Assert.That(minimumFootClearance, Is.GreaterThan(-0.08f),
                "The platform top may not pass through the rider before depenetration pushes them out.");
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(platform.SurfaceId));
            if (puppet != null)
                Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(Elemental.Simulation.Characters.CharacterPhysicalMode.FullRagdoll));

            PlatformMotorInputSource scripted = motor.gameObject.AddComponent<PlatformMotorInputSource>();
            motor.ConfigureInputSource(scripted);
            Vector3 walkStart = rider.position;
            scripted.Move = new float2(0f, 0.55f);
            for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
            scripted.Move = float2.zero;
            float platformWalkDistance = Vector3.ProjectOnPlane(rider.position - walkStart, up).magnitude;
            Assert.That(platformWalkDistance, Is.GreaterThan(0.12f),
                "A stationary moving-surface session must preserve the player's relative locomotion.");
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(platform.SurfaceId));

            EarthPillarMobility pillar = motor.GetComponent<EarthPillarMobility>();
            Assert.That(pillar, Is.Not.Null);
            Assert.That(pillar.BeginCharge(), Is.True,
                "An emerging or settled owned platform must count as valid pillar-jump support.");
            yield return new WaitForSeconds(0.18f);
            Assert.That(pillar.ReleaseCharge(), Is.True);
            Assert.That(pillar.LastLaunchSurface.Handle.Kind, Is.EqualTo(EarthSurfaceKind.Platform));
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Dot(rider.linearVelocity, up), Is.GreaterThan(0.5f),
                "The pillar jump must launch upward from the platform surface.");

            bool descending = false;
            bool landedOnPlatform = false;
            float minimumDescentClearance = float.PositiveInfinity;
            for (int tick = 0; tick < 240; tick++)
            {
                yield return new WaitForFixedUpdate();
                float verticalSpeed = Vector3.Dot(rider.linearVelocity, up);
                float clearance = Vector3.Dot(
                    motor.SupportFeetPoint(up) - platform.SurfaceTopPoint,
                    up);
                if (verticalSpeed < -0.25f) descending = true;
                if (descending) minimumDescentClearance = Mathf.Min(minimumDescentClearance, clearance);
                if (descending && motor.HasStableSupport && clearance < 0.25f)
                {
                    landedOnPlatform = true;
                    break;
                }
            }
            Assert.That(landedOnPlatform, Is.True,
                "The restored platform collision must catch the complete pillar-jump arc.");
            Assert.That(minimumDescentClearance, Is.GreaterThan(-0.14f),
                "The actor may not tunnel back through the platform after a pillar jump.");

            GameObject launchPillar = null;
            int activeLaunchChips = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Rising Earth Pillar") launchPillar = child.gameObject;
                if (child.name.StartsWith("Lift Ground Chip") && child.gameObject.activeSelf)
                    activeLaunchChips++;
            }
            Assert.That(launchPillar, Is.Not.Null);
            Assert.That(launchPillar.activeSelf, Is.False,
                "The jump pillar must retreat before the player returns through its old volume.");
            Assert.That(activeLaunchChips, Is.Zero,
                "Every launch chip must keep moving and shrink to completion instead of hanging in mid-air.");

            Assert.That(platform.ApplyStructureImpact(
                platform.transform.position + up * platform.Height,
                forward,
                2200f), Is.True);
            yield return new WaitForFixedUpdate();
            MeshFilter authoredPiece = platform.FirstActivePiece != null
                ? platform.FirstActivePiece.GetComponent<MeshFilter>()
                : null;
            Assert.That(authoredPiece, Is.Not.Null);
            Assert.That(authoredPiece.sharedMesh.name, Does.Not.StartWith("Debug Platform Piece"));
            Assert.That(authoredPiece.sharedMesh.vertexCount, Is.GreaterThan(8));

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator BallisticRockDebrisKeepsMovingWhileItShrinks()
        {
            GameObject pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Rigidbody body = pieceObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            EarthRockDebris debris = pieceObject.AddComponent<EarthRockDebris>();
            EarthRockProfile profile = ScriptableObject.CreateInstance<EarthRockProfile>();
            debris.BeginBallistic(Vector3.zero, 0.4f, 2f, new Vector3(3f, -1f, 0f), profile);
            Vector3 fullScale = pieceObject.transform.localScale;

            yield return new WaitForSeconds(1.3f);
            Vector3 movingPosition = pieceObject.transform.position;
            Vector3 shrinkingScale = pieceObject.transform.localScale;
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.detectCollisions, Is.True);
            Assert.That(pieceObject.GetComponent<Collider>().enabled, Is.True);
            Assert.That(shrinkingScale.magnitude, Is.LessThan(fullScale.magnitude));
            Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(0.5f));

            yield return new WaitForSeconds(0.12f);
            Assert.That(Vector3.Distance(pieceObject.transform.position, movingPosition), Is.GreaterThan(0.04f));
            Assert.That(pieceObject.transform.localScale.magnitude, Is.LessThan(shrinkingScale.magnitude));

            Object.Destroy(pieceObject);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator HeldRectangularEarthMassLevelsAndDampsItsSpin()
        {
            GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.transform.rotation = Quaternion.Euler(72f, 18f, 25f);
            Rigidbody body = stone.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.angularVelocity = new Vector3(7f, -4f, 5f);
            EarthHoverProfile profile = ScriptableObject.CreateInstance<EarthHoverProfile>();
            EarthHoverFrame frame = EarthHoverPhysics.Capture(body, Vector3.up, 17u);
            for (int index = 0; index < 90; index++)
            {
                EarthHoverPhysics.Stabilize(body, in frame, Vector3.up, Time.fixedTime, profile);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector3.Angle(stone.transform.up, Vector3.up), Is.LessThan(6f));
            Assert.That(body.angularVelocity.magnitude, Is.LessThan(1.0f));

            Object.Destroy(stone);
            Object.Destroy(profile);
        }

        [UnityTest]
        public IEnumerator WallChordBottomSamplesStayInsidePlanetSurface()
        {
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            EarthWall wall = wallObject.AddComponent<EarthWall>();
            float angle = 22f * Mathf.Deg2Rad;
            Vector3 start = new Vector3(-Mathf.Sin(angle) * 24f, Mathf.Cos(angle) * 24f, 0f);
            Vector3 end = new Vector3(Mathf.Sin(angle) * 24f, Mathf.Cos(angle) * 24f, 0f);
            wall.Initialize(1u, start, end, Vector3.zero, 3f, 0.7f);
            yield return new WaitForSeconds(0.75f);

            for (int along = 0; along <= 12; along++)
            for (int depth = 0; depth <= 4; depth++)
            {
                Vector3 point = wall.GetBasePoint(along / 12f, depth / 4f);
                Assert.That(point.magnitude, Is.LessThanOrEqualTo(23.53f));
            }

            wall.OnEarthMagicGrabbed(EarthMagicGripKind.VectorField);
            wall.ApplyMagicPush(Vector3.forward, 9000f);
            yield return new WaitForSeconds(0.45f);
            for (int along = 0; along <= 12; along++)
            for (int depth = 0; depth <= 4; depth++)
            {
                Vector3 point = wall.GetBasePoint(along / 12f, depth / 4f);
                Assert.That(point.magnitude, Is.LessThanOrEqualTo(23.56f));
            }

            Object.Destroy(wallObject);
        }

        [UnityTest]
        public IEnumerator PillarWaveColumnIgnoresItsCasterUntilItReturnsToPool()
        {
            GameObject columnObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Rigidbody columnBody = columnObject.AddComponent<Rigidbody>();
            columnBody.useGravity = false;
            EarthPillarWaveColumn column = columnObject.AddComponent<EarthPillarWaveColumn>();
            Collider columnCollider = columnObject.GetComponent<Collider>();

            GameObject casterObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Rigidbody casterBody = casterObject.AddComponent<Rigidbody>();
            casterBody.useGravity = false;
            Collider casterCollider = casterObject.GetComponent<Collider>();

            column.Schedule(
                null,
                Vector3.zero,
                Vector3.up,
                Vector3.forward,
                2f,
                1f,
                1f,
                0f,
                0.05f,
                1f,
                1u,
                100f,
                casterBody,
                null,
                new Collider[4]);
            yield return new WaitForFixedUpdate();

            Assert.That(UnityEngine.Physics.GetIgnoreCollision(columnCollider, casterCollider), Is.True);
            Assert.That(casterBody.linearVelocity.magnitude, Is.LessThan(0.01f));

            column.ResetColumn();
            Assert.That(UnityEngine.Physics.GetIgnoreCollision(columnCollider, casterCollider), Is.False);

            Object.Destroy(columnObject);
            Object.Destroy(casterObject);
        }

        [UnityTest]
        public IEnumerator RaisedPillarCanBeControlledThenFlickedAsAProjectile()
        {
            GameObject columnObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Rigidbody columnBody = columnObject.AddComponent<Rigidbody>();
            columnBody.useGravity = false;
            EarthPillarWaveColumn column = columnObject.AddComponent<EarthPillarWaveColumn>();
            column.Schedule(
                null, Vector3.zero, Vector3.up, Vector3.forward,
                2f, 1f, 1f, 0f, 5f, 1f, 17u, 0f, null, null, new Collider[4]);
            yield return new WaitForSeconds(0.25f);

            EarthMatterIdentity columnMatter = column.GetComponent<EarthMatterIdentity>();
            Assert.That(columnMatter, Is.Not.Null);
            Assert.That(columnMatter.TryRead(out Elemental.Simulation.Matter.EarthMatterRecord columnRecord), Is.True);
            Assert.That(columnRecord.Shape, Is.EqualTo(Elemental.Simulation.Matter.EarthShapeSemantic.Pillar));

            GameObject executorObject = new GameObject("Pillar Vector Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            Vector3 start = columnBody.position;
            Assert.That(executor.TryBeginVectorField(
                columnObject.GetComponent<Collider>(), columnBody,
                columnBody.worldCenterOfMass, Vector3.forward), Is.True);
            for (int tick = 0; tick < 12; tick++)
            {
                executor.UpdateVectorField(Vector3.forward, 0.35f);
                yield return new WaitForFixedUpdate();
            }
            float controlledSpeed = columnBody.linearVelocity.z;
            Assert.That(controlledSpeed, Is.GreaterThan(0.1f));
            Assert.That(controlledSpeed, Is.LessThanOrEqualTo(9.1f));

            Assert.That(executor.ReleaseVectorField(
                EarthVectorReleaseIntent.ProjectileFlick, Vector3.forward, 1f), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(columnBody.isKinematic, Is.False);
            Assert.That(columnBody.linearVelocity.z, Is.GreaterThan(controlledSpeed));
            Assert.That(Vector3.Distance(start, columnBody.position), Is.GreaterThan(0.05f));

            Object.Destroy(executorObject);
            Object.Destroy(columnObject);
        }

        [UnityTest]
        public IEnumerator FallingSpaceCushionCapsDescentWithoutInjectingImpactDamage()
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.transform.localScale = Vector3.one * 48f;
            GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            actor.transform.position = new Vector3(0f, 32f, 0f);
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(0f, -18f, 0f);
            PlanetMotor motor = actor.AddComponent<PlanetMotor>();
            motor.enabled = false;
            PhysicalImpactTarget impact = actor.AddComponent<PhysicalImpactTarget>();
            impact.Configure(body);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(visual.GetComponent<Collider>());
            EarthLandingCushionProfile profile = ScriptableObject.CreateInstance<EarthLandingCushionProfile>();
            EarthLandingCushion cushion = actor.AddComponent<EarthLandingCushion>();
            cushion.Configure(body, motor, null, planet.GetComponent<Collider>(), profile, visual.transform);

            Assert.That(cushion.BeginHold(), Is.True);
            yield return new WaitForSeconds(0.75f);

            float downSpeed = -Vector3.Dot(body.linearVelocity, Vector3.up);
            Assert.That(downSpeed, Is.LessThanOrEqualTo(4.1f));
            Assert.That(impact.AccumulatedImpulse, Is.EqualTo(0f).Within(0.001f));
            Assert.That(visual.activeSelf, Is.True);

            Object.Destroy(planet);
            Object.Destroy(actor);
            Object.Destroy(visual);
            Object.Destroy(profile);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
