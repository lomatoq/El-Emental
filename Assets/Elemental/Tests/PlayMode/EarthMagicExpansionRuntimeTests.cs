using System.Collections;
using System.Collections.Generic;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthMagicExpansionRuntimeTests
    {
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
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(40));
            int activeBefore = wall.ActiveFracturePieceCount;
            yield return new WaitForSeconds(3f);
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(activeBefore),
                "Latched fracture targets must not shrink or return to the pool while MMB is held.");
            Assert.That(executor.GravityWellCapturedCount, Is.GreaterThanOrEqualTo(40));

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
            yield return new WaitForSeconds(1.1f);
            float liftedRadius = Vector3.Distance(rider.worldCenterOfMass, planet.transform.position);

            Assert.That(platform.IsFractured, Is.False);
            Assert.That(liftedRadius, Is.GreaterThan(initialRadius + 0.35f));
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(platform.SurfaceId));
            if (puppet != null)
                Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(Elemental.Simulation.Characters.CharacterPhysicalMode.FullRagdoll));

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
