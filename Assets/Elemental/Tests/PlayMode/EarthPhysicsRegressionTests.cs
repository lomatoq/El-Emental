using System.Collections;
using Elemental.Runtime.Physics;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthPhysicsRegressionTests
    {
        [UnityTest]
        public IEnumerator EarthMvpWallDefaultsRequireCauseAndKeepStructuralPiecesPersistent()
        {
            EarthWallProfile profile = ScriptableObject.CreateInstance<EarthWallProfile>();

            Assert.That(profile.AutomaticCrackDelaySeconds, Is.Zero);
            Assert.That(profile.ShrinkDetachedStructuralPieces, Is.False);

            Object.Destroy(profile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FragmentPoolRejectsOverflowWithoutOverwritingLiveMatter()
        {
            GameObject root = new GameObject("Fragment Pool Stress");
            root.SetActive(false);
            EarthFragmentPool pool = root.AddComponent<EarthFragmentPool>();
            pool.Configure(4, null, null);
            root.SetActive(true);

            int accepted = 0;
            for (int index = 0; index < 100; index++)
                if (pool.Acquire(null, new Vector3(index * 0.01f, 20f, 0f), 0.5f, 10f) != null)
                    accepted++;

            Assert.That(root.transform.childCount, Is.EqualTo(4));
            Assert.That(pool.ActiveCount, Is.EqualTo(4));
            Assert.That(accepted, Is.EqualTo(4));
            Assert.That(pool.LastAcquired.FragmentId, Is.EqualTo(4u));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RockAccretionGrowsMassAndHeavyImpactUsesPooledShatterDebris()
        {
            EarthRockProfile profile = ScriptableObject.CreateInstance<EarthRockProfile>();
            GameObject root = new GameObject("Rock Lifecycle Pool");
            root.SetActive(false);
            EarthRockDebrisPool debris = root.AddComponent<EarthRockDebrisPool>();
            debris.Configure(16, null, null, null, profile);
            EarthFragmentPool fragments = root.AddComponent<EarthFragmentPool>();
            fragments.Configure(2, null, null, null, profile, debris);
            root.SetActive(true);

            EarthFragment rock = fragments.Acquire(null, new Vector3(0f, 5f, 0f), 0.65f, 80f);
            float originalRadius = rock.Radius;
            float originalMass = rock.Mass;
            rock.AccreteVolume(0.5f);

            Assert.That(rock.Radius, Is.GreaterThan(originalRadius));
            Assert.That(rock.Mass, Is.GreaterThan(originalMass));
            Assert.That(rock.TryShatter(rock.transform.position, Vector3.up, 10000f), Is.True);
            Assert.That(rock.gameObject.activeSelf, Is.False);
            Assert.That(root.GetComponentsInChildren<EarthRockDebris>().Length,
                Is.EqualTo(profile.ShatterPieceCount));

            Object.Destroy(root);
            Object.Destroy(profile);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EqualMagicImpulseSlidesSmallWallFartherThanHeavyWall()
        {
            GameObject root = new GameObject("Wall Mass Push Comparison");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(2, null, null);
            root.SetActive(true);
            EarthWall small = pool.Acquire(
                new Vector3(-7f, 12f, 0f), new Vector3(-5f, 12f, 0f),
                Vector3.zero, 1.5f, 0.45f);
            EarthWall large = pool.Acquire(
                new Vector3(2f, 12f, 0f), new Vector3(10f, 12f, 0f),
                Vector3.zero, 4f, 0.65f);
            yield return new WaitForSeconds(0.75f);

            float smallVelocityChange = small.ApplyMagicPush(Vector3.forward, 900f);
            float largeVelocityChange = large.ApplyMagicPush(Vector3.forward, 900f);
            Vector3 smallBefore = small.transform.position;
            Vector3 largeBefore = large.transform.position;
            yield return new WaitForSeconds(0.2f);

            Assert.That(smallVelocityChange, Is.GreaterThan(largeVelocityChange * 4f));
            float smallTravel = Vector3.Distance(small.transform.position, smallBefore);
            float largeTravel = Vector3.Distance(large.transform.position, largeBefore);
            Assert.That(smallTravel,
                Is.GreaterThan(largeTravel * 2f),
                $"smallTravel={smallTravel:F3}, largeTravel={largeTravel:F3}, " +
                $"smallMass={small.EstimatedMass:F1}, largeMass={large.EstimatedMass:F1}, " +
                $"smallVelocity={small.Body.linearVelocity}, largeVelocity={large.Body.linearVelocity}, " +
                $"smallBefore={smallBefore}, smallAfter={small.transform.position}, " +
                $"largeBefore={largeBefore}, largeAfter={large.transform.position}");
            Assert.That(Vector3.Angle(small.transform.up, small.transform.position.normalized),
                Is.LessThan(0.75f),
                "Sliding walls must stay visibly upright and remain below the 1 degree acceptance limit.");

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WallPoolRaisesRectangularCollidersWithoutGrowingTerrainEditCost()
        {
            GameObject root = new GameObject("Wall Pool Stress");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(3, null, null);
            int collapsedEvents = 0;
            pool.WallCollapsed += _ => collapsedEvents++;
            root.SetActive(true);

            for (int index = 0; index < 40; index++)
            {
                float offset = ((index % 3) - 1) * 2f;
                EarthWall acquired = pool.Acquire(
                    new Vector3(-3f, 24f, offset),
                    new Vector3(3f, 24f, offset),
                    Vector3.zero,
                    2.5f,
                    0.55f);
                Assert.That(acquired, Is.Not.Null);
                if (index < 39) Assert.That(pool.ReleaseTransient(acquired), Is.True);
            }

            Assert.That(root.transform.childCount, Is.EqualTo(3));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.LastAcquired.WallId, Is.EqualTo(40u));
            BoxCollider wallCollider = pool.LastAcquired.GetComponent<BoxCollider>();
            Assert.That(wallCollider, Is.Not.Null);
            Assert.That(wallCollider.enabled, Is.False, "A buried wall must not create invisible collision.");
            Vector3 stableRootPosition = pool.LastAcquired.transform.position;
            float buriedVisualY = pool.LastAcquired.VisualEmergenceRoot.localPosition.y;
            Assert.That(pool.LastAcquired.transform.localScale.x, Is.EqualTo(6f).Within(0.01f));

            yield return new WaitForSeconds(0.65f);
            Assert.That(pool.LastAcquired.VisualEmergenceRoot.localPosition.y,
                Is.GreaterThan(buriedVisualY + 0.8f));
            Assert.That(Vector3.Distance(pool.LastAcquired.transform.position, stableRootPosition),
                Is.LessThan(0.02f), "Emergence must never animate the Rigidbody root.");
            Assert.That(pool.LastAcquired.PeakRootEmergenceDisplacementMeters, Is.LessThan(0.02f));
            Assert.That(pool.LastAcquired.transform.localScale.y, Is.EqualTo(2.5f).Within(0.01f));
            Assert.That(wallCollider.enabled, Is.True);
            Assert.That(pool.LastAcquired.PeakEmergenceTremorMeters, Is.GreaterThan(0.08f));
            float wallVelocityChange = pool.LastAcquired.ApplyMagicPush(Vector3.forward, 1150f);
            Assert.That(wallVelocityChange, Is.InRange(0.1f, 3.5f),
                "A large wall must respond to magic push but resist it through its estimated mass.");
            Vector3 positionBeforePush = pool.LastAcquired.transform.position;
            yield return new WaitForSeconds(0.18f);
            Assert.That(Vector3.Distance(pool.LastAcquired.transform.position, positionBeforePush),
                Is.GreaterThan(0.08f),
                $"The anchored shove must be visible, not just telemetry. " +
                $"velocity={pool.LastAcquired.Body.linearVelocity}, kinematic={pool.LastAcquired.Body.isKinematic}, " +
                $"before={positionBeforePush}, after={pool.LastAcquired.transform.position}.");
            Assert.That(Vector3.Angle(
                    pool.LastAcquired.transform.up,
                    pool.LastAcquired.transform.position.normalized), Is.LessThan(0.1f),
                "A magic shove should slide the wall without making it lean.");

            yield return new WaitForSeconds(3.75f);
            Assert.That(pool.LastAcquired.IsCollapsing, Is.False,
                "An undamaged MVP wall must remain stable instead of crumbling on a timer.");
            Assert.That(wallCollider.enabled, Is.True);
            Assert.That(pool.LastAcquired.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(collapsedEvents, Is.Zero);

            Assert.That(pool.LastAcquired.ApplyRockImpact(
                pool.LastAcquired.transform.position,
                Vector3.forward,
                100f), Is.True);
            yield return new WaitForSeconds(0.12f);
            Assert.That(pool.LastAcquired.IsCollapsing, Is.True);
            Assert.That(wallCollider.enabled, Is.False);
            Assert.That(pool.LastAcquired.VisualEmergenceRoot.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(pool.LastAcquired.ActiveFracturePieceCount, Is.EqualTo(40));
            Assert.That(pool.LastAcquired.RemainingBondCount, Is.GreaterThan(0),
                "Impact fracture should begin as a cohesive cluster, not instant confetti.");
            Assert.That(pool.LastAcquired.FirstFracturePiece.parent, Is.EqualTo(root.transform),
                "Fracture pieces must detach from the non-uniformly scaled wall before tumbling.");
            Rigidbody firstPieceBody = pool.LastAcquired.FirstFracturePiece.GetComponent<Rigidbody>();
            Assert.That(firstPieceBody, Is.Not.Null);
            yield return new WaitForSeconds(0.18f);
            Assert.That(firstPieceBody.isKinematic, Is.False,
                "Fractured Voronoi chunks remain physical while bonds still hold them together.");
            Assert.That(firstPieceBody.detectCollisions, Is.True);
            Vector3 fullPieceScale = firstPieceBody.transform.localScale;
            yield return new WaitForSeconds(2.15f);
            Assert.That(firstPieceBody.gameObject.activeSelf, Is.True,
                "Repairable structural pieces must remain present after fracture.");
            Assert.That(firstPieceBody.transform.localScale, Is.EqualTo(fullPieceScale),
                "Repairable structural pieces must not shrink while they remain interactable.");
            Assert.That(collapsedEvents, Is.EqualTo(1), "The explicitly fractured wall should emit exactly once.");

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DebrisPoolUsesConvexIrregularMeshesWithoutPrimitiveSphereFallback()
        {
            GameObject root = new GameObject("Irregular Debris Pool");
            root.SetActive(false);
            EarthRockDebrisPool pool = root.AddComponent<EarthRockDebrisPool>();
            pool.Configure(16, null, null, null, null);
            root.SetActive(true);
            yield return null;

            Transform first = root.transform.GetChild(0);
            MeshCollider collider = first.GetComponent<MeshCollider>();
            Assert.That(first.GetComponent<SphereCollider>(), Is.Null);
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.convex, Is.True);
            Assert.That(collider.sharedMesh, Is.Not.Null);
            Assert.That(collider.sharedMesh.name,
                Does.Contain("EarthRock_").Or.Contain("Irregular Earth Debris"));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RockImpactImmediatelyFracturesAStandingWall()
        {
            GameObject root = new GameObject("Impact Fracture Wall Pool");
            root.SetActive(false);
            EarthWallPool pool = root.AddComponent<EarthWallPool>();
            pool.Configure(1, null, null);
            root.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-2f, 10f, 0f),
                new Vector3(2f, 10f, 0f),
                Vector3.zero,
                2.5f,
                0.55f);

            yield return new WaitForSeconds(0.55f);
            bool fractured = wall.ApplyRockImpact(wall.transform.position, Vector3.forward, 100f);

            Assert.That(fractured, Is.True);
            Assert.That(wall.IsCollapsing, Is.True);
            Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(wall.ActiveFracturePieceCount, Is.EqualTo(40));
            Assert.That(wall.RemainingBondCount, Is.GreaterThan(0));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightMousePushAppliesMassIndependentVelocityToDynamicTarget()
        {
            GameObject cameraObject = new GameObject("Push Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = Vector3.zero;
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject inputObject = new GameObject("Push Test Input");
            inputObject.SetActive(false);
            MagicInputController input = inputObject.AddComponent<MagicInputController>();
            input.Configure(null, camera, null, null, null);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Push Test Target";
            target.transform.position = new Vector3(0f, 0f, 5f);
            Rigidbody body = target.AddComponent<Rigidbody>();
            body.mass = 250f;
            body.useGravity = false;
            Physics.SyncTransforms();

            GameObject executorObject = new GameObject("Push Test Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            input.Configure(null, camera, executor, null, null);
            bool pushed = input.TryReleasePushAtScreenPoint(
                new float2(Screen.width * 0.5f, Screen.height * 0.5f), 0.1f);
            yield return new WaitForFixedUpdate();

            Assert.That(pushed, Is.True);
            Assert.That(body.linearVelocity.z, Is.GreaterThan(0.5f));

            Object.Destroy(cameraObject);
            Object.Destroy(inputObject);
            Object.Destroy(executorObject);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssistedPushRayFindsAndVisiblyShovesAThinWall()
        {
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "Assisted Push Wall";
            wallObject.transform.position = new Vector3(0.75f, 0f, 5f);
            EarthWall wall = wallObject.AddComponent<EarthWall>();
            wall.Initialize(
                1u,
                new Vector3(0.6f, 0f, 5f),
                new Vector3(0.9f, 0f, 5f),
                new Vector3(0.75f, -10f, 5f),
                2.5f,
                0.4f);

            GameObject cameraObject = new GameObject("Wall Push Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 1.25f, 0f);
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject executorObject = new GameObject("Wall Push Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();
            GameObject inputObject = new GameObject("Wall Push Input");
            inputObject.SetActive(false);
            MagicInputController input = inputObject.AddComponent<MagicInputController>();
            input.Configure(null, camera, executor, null, null);

            yield return new WaitForSeconds(0.55f);
            Vector3 before = wall.transform.position;
            bool pushed = input.TryReleasePushAtScreenPoint(
                new float2(Screen.width * 0.5f, Screen.height * 0.5f), 2f);
            yield return new WaitForSeconds(0.28f);

            Assert.That(pushed, Is.True, "The assisted push volume should catch a thin wall beside the exact ray.");
            Assert.That(Vector3.Distance(wall.transform.position, before), Is.GreaterThan(0.12f));

            Object.Destroy(wallObject);
            Object.Destroy(cameraObject);
            Object.Destroy(executorObject);
            Object.Destroy(inputObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AppliedImpulsePreservesExpectedLinearMomentum()
        {
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Momentum Regression Target";
            Rigidbody body = targetObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 10f;
            PhysicalImpactTarget target = targetObject.AddComponent<PhysicalImpactTarget>();
            target.Configure(body, 1f);

            target.ApplyImpact(body.worldCenterOfMass, Vector3.right, 100f);
            yield return new WaitForFixedUpdate();

            float forwardMomentum = body.mass * Vector3.Dot(body.linearVelocity, Vector3.right);
            Assert.That(forwardMomentum, Is.EqualTo(100f).Within(0.1f));
            Assert.That(target.AccumulatedImpulse, Is.EqualTo(100f).Within(0.001f));

            Object.Destroy(targetObject);
            yield return null;
        }
    }
}
