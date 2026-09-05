using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSurfRuntimeTests
    {
        [UnityTest]
        public IEnumerator ReleasedStonesKeepMovingWhenAnotherBoardStarts()
        {
            GameObject casterObject = new GameObject("Surf Release Visual Test");
            casterObject.transform.position = Vector3.up * 25f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.isKinematic = true;
            caster.useGravity = false;
            casterObject.AddComponent<CapsuleCollider>();
            EarthSurfController surf = casterObject.AddComponent<EarthSurfController>();
            surf.Configure(caster, null, null, null, null);
            Assert.That(surf.Begin(Time.fixedUnscaledTime, Vector3.forward), Is.True);
            yield return new WaitForSeconds(0.32f);
            surf.Cancel();
            var before = new Vector3[48];
            var after = new Vector3[48];
            Assert.That(surf.CopyReleasedStonePositionsNonAlloc(before), Is.EqualTo(12));
            Assert.That(surf.Begin(Time.fixedUnscaledTime, Vector3.forward), Is.True);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(surf.CopyReleasedStonePositionsNonAlloc(after), Is.EqualTo(12),
                "Starting another board must not hide or reuse the previous release's visible stones.");
            Assert.That(Vector3.Distance(before[0], after[0]), Is.GreaterThan(0.005f));
            TrailRenderer trail = surf.BoardTransform.GetComponent<TrailRenderer>();
            Assert.That(trail.emitting, Is.False);
            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageDetachesPrebuiltOuterCellsButKeepsOccupiedCore()
        {
            GameObject casterObject = new GameObject("Finite Surf Test Caster");
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.isKinematic = true;
            casterObject.AddComponent<CapsuleCollider>();
            EarthSurfController surf = casterObject.AddComponent<EarthSurfController>();
            surf.Configure(caster, null, null, null, null);
            Assert.That(surf.Begin(Time.fixedUnscaledTime, Vector3.forward), Is.True);

            GameObject board = surf.BoardTransform != null ? surf.BoardTransform.gameObject : null;
            Assert.That(board, Is.Not.Null);
            Assert.That(board.GetComponentsInChildren<MeshCollider>(true), Is.Empty,
                "Finite surf cells must be prebuilt views; damage cannot cook mesh colliders.");
            Assert.That(CountActiveSemanticCells(), Is.EqualTo(EarthSurfCellGraph.CellCount));

            bool applied = surf.ApplyIntegrityEvent(
                EarthSurfDamageKind.Bump,
                4.2f,
                30f,
                -0.8f,
                board.transform.position - board.transform.right,
                -board.transform.forward + board.transform.up);
            Assert.That(applied, Is.True);
            Assert.That(surf.DetachedOuterCellCount, Is.InRange(1, 3));
            Assert.That(surf.AttachedCellMask & EarthSurfCellGraph.SupportCoreMask,
                Is.EqualTo(EarthSurfCellGraph.SupportCoreMask));
            Assert.That(CountAttachedActiveSemanticCells(board.transform),
                Is.EqualTo(EarthSurfCellGraph.CellCount - surf.DetachedOuterCellCount));
            ParticleSystem dust = board.GetComponent<ParticleSystem>();
            Assert.That(dust, Is.Not.Null);
            Assert.That(dust.particleCount, Is.GreaterThan(0));

            surf.ApplyIntegrityEvent(
                EarthSurfDamageKind.NoseCrash,
                12f,
                0f,
                0f,
                board.transform.position + board.transform.forward,
                -board.transform.forward + board.transform.up * 0.4f);
            Assert.That(surf.IsEmerging, Is.False,
                "A severe nose/wall crash must begin the surf release instead of riding forever.");
            Assert.That(surf.AttachedCellMask & EarthSurfCellGraph.SupportCoreMask,
                Is.EqualTo(EarthSurfCellGraph.SupportCoreMask));

            Object.Destroy(casterObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShiftMovementSurfFindsSupportEmergesAndMovesTangentially()
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "Surf Test Planet";
            planet.transform.localScale = Vector3.one * 48f;
            Physics.SyncTransforms();

            GameObject casterObject = new GameObject("Surf Test Caster");
            casterObject.transform.position = Vector3.up * 24.9f;
            Rigidbody caster = casterObject.AddComponent<Rigidbody>();
            caster.useGravity = false;
            caster.isKinematic = true;
            CapsuleCollider casterCollider = casterObject.AddComponent<CapsuleCollider>();
            casterCollider.height = 1.8f;
            casterCollider.radius = 0.35f;

            EarthSurfController surf = casterObject.AddComponent<EarthSurfController>();
            surf.Configure(caster, null, planet.transform, null, null);
            Physics.SyncTransforms();
            Assert.That(surf.HasNearbyStartSurface(), Is.True,
                "Shift+movement must accept a stable planet surface even if the motor ground bit flickers.");
            Assert.That(surf.Begin(Time.fixedUnscaledTime, Vector3.forward), Is.True);
            Assert.That(surf.BoardTransform, Is.Not.Null);
            Vector3 startedAt = surf.BoardTransform.position;

            for (int frame = 0; frame < 12; frame++) yield return new WaitForFixedUpdate();

            GameObject board = surf.BoardTransform != null ? surf.BoardTransform.gameObject : null;
            Assert.That(board, Is.Not.Null);
            Assert.That(surf.IsActive, Is.True);
            Assert.That(surf.Speed, Is.GreaterThanOrEqualTo(4f));
            Mesh boardMesh = board.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(boardMesh.bounds.size.x, Is.GreaterThan(2.2f));
            Assert.That(boardMesh.bounds.size.z, Is.GreaterThan(3.7f),
                "The surf spell needs a readable plough platform, not a tiny foot wedge.");
            Assert.That(Vector3.Distance(startedAt, board.transform.position), Is.GreaterThan(0.45f),
                "The plough must actually carry forward, not only spawn under the character.");
            Assert.That(Mathf.Abs(Vector3.Distance(board.transform.position, planet.transform.position) - 24f),
                Is.LessThan(0.6f), "Surf movement must follow planet curvature.");
            int activeCutChips = 0;
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name.StartsWith("Surf Cut Chip") &&
                    transforms[index].gameObject.activeSelf) activeCutChips++;
            Assert.That(activeCutChips, Is.GreaterThanOrEqualTo(3),
                "The plough needs a dense spray of moving cut stones, not one occasional chip.");

            surf.Release(Time.fixedUnscaledTime);
            for (int frame = 0; frame < 30; frame++) yield return new WaitForFixedUpdate();
            Assert.That(surf.IsActive, Is.False);

            Object.Destroy(casterObject);
            Object.Destroy(planet);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionPloughCarriesTheVisibleHeroInsteadOfLeavingHimBehind()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthSceneReadinessGate gate = FindInScene<EarthSceneReadinessGate>(scene);
            for (int frame = 0; frame < 2400 && gate != null && !gate.IsReady && !gate.Failed; frame++)
                yield return null;
            Assert.That(gate, Is.Not.Null);
            Assert.That(gate.Failed, Is.False);
            Assert.That(gate.IsReady, Is.True);
            EarthMvpBotController rival = FindInScene<EarthMvpBotController>(scene);
            if (rival != null) rival.enabled = false;
            EarthSurfController surf = FindInScene<EarthSurfController>(scene);
            PlanetMotor motor = surf != null ? surf.GetComponent<PlanetMotor>() : null;
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            ActiveRagdollPuppet puppet = motor != null ? motor.GetComponent<ActiveRagdollPuppet>() : null;
            Animator animator = motor != null ? motor.GetComponentInChildren<Animator>(true) : null;
            Assert.That(surf, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            Vector3 startedAt = body.worldCenterOfMass;
            Vector3 up = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : motor.transform.up;
            Assert.That(surf.Begin(Time.fixedUnscaledTime, motor.FacingForward), Is.True);
            for (int tick = 0; tick < 36; tick++) yield return new WaitForFixedUpdate();

            Vector3 displacement = body.worldCenterOfMass - startedAt;
            float tangentTravel = Vector3.ProjectOnPlane(displacement, up).magnitude;
            Assert.That(tangentTravel, Is.GreaterThan(0.8f),
                "Shift+movement must carry the hero with the wedge; moving only the effect is not gameplay.");
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(surf.SurfaceId),
                $"surf active={surf.IsActive} emerging={surf.IsEmerging} speed={surf.Speed:F2} " +
                $"integrity={surf.BoardIntegrity:F1} cells={surf.AttachedCellMask:X4} " +
                $"acceptsSupport={motor.AcceptsMovingSupport} motorState={motor.MotionState} " +
                $"lastTarget={surf.LastIntegrityTargetName}");
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.GetBool("Surfing"), Is.True);
            AnimatorStateInfo surfState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(surfState.IsName("Surf Crouch") || surfState.IsName("Surf Enter") ||
                        surfState.IsName("Base Layer.Surf Crouch") || surfState.IsName("Base Layer.Surf Enter"),
                Is.True, "The surf wedge must own a crouched base pose instead of a T-pose or walking legs.");
            if (puppet != null)
                Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(
                    Elemental.Simulation.Characters.CharacterPhysicalMode.FullRagdoll));

            surf.Cancel();
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionSurfPillarJumpRaisesOnePillarBreaksBoardAndLaunchesHero()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthSceneReadinessGate gate = FindInScene<EarthSceneReadinessGate>(scene);
            for (int frame = 0; frame < 2400 && gate != null && !gate.IsReady && !gate.Failed; frame++)
                yield return null;
            Assert.That(gate != null && gate.IsReady, Is.True);
            EarthMvpBotController rival = FindInScene<EarthMvpBotController>(scene);
            if (rival != null) rival.enabled = false;
            EarthSurfController surf = FindInScene<EarthSurfController>(scene);
            PlanetMotor motor = surf != null ? surf.GetComponent<PlanetMotor>() : null;
            Rigidbody body = motor != null ? motor.GetComponent<Rigidbody>() : null;
            EarthPillarMobility pillar = motor != null ? motor.GetComponent<EarthPillarMobility>() : null;
            Elemental.Input.Actions.EarthActionRouterBehaviour router =
                motor != null ? motor.GetComponent<Elemental.Input.Actions.EarthActionRouterBehaviour>() : null;
            for (int frame = 0; frame < 100 && motor != null && !motor.IsGrounded; frame++)
                yield return new WaitForFixedUpdate();

            Assert.That(surf, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(pillar, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(surf.Begin(Time.fixedUnscaledTime, motor.FacingForward), Is.True);
            for (int frame = 0; frame < 12 && motor.MovingSurfaceId != surf.SurfaceId; frame++)
                yield return new WaitForFixedUpdate();
            Assert.That(motor.MovingSurfaceId, Is.EqualTo(surf.SurfaceId));

            uint routeSequence = router.SurfPillarJumpSequence;
            uint breakSequence = surf.PillarJumpBreakSequence;
            int raisedCount = 0;
            Vector3 raisedDirection = Vector3.zero;
            pillar.PillarRaised += value =>
            {
                raisedCount++;
                raisedDirection = new Vector3(value.Direction.x, value.Direction.y, value.Direction.z);
            };
            Assert.That(router.TryBeginSurfPillarJumpCharge(), Is.True);
            double releaseAt = Time.realtimeSinceStartupAsDouble + 0.55d;
            while (Time.realtimeSinceStartupAsDouble < releaseAt) yield return null;
            float heldCharge = pillar.Charge01;
            Assert.That(pillar.IsCharging, Is.True);
            Assert.That(heldCharge, Is.GreaterThan(0.25f));
            Vector3 up = motor.LocalUp.normalized;
            Vector3 travel = Vector3.ProjectOnPlane(surf.SurfaceVelocity, up);
            if (travel.sqrMagnitude < 0.25f)
                travel = Vector3.ProjectOnPlane(motor.FacingForward, up);
            travel.Normalize();
            Vector3 start = body.worldCenterOfMass;
            Assert.That(router.TryReleaseSurfPillarJump(motor.FacingForward), Is.True);
            Assert.That(router.TryReleaseSurfPillarJump(motor.FacingForward), Is.False,
                "One Space release may schedule exactly one pillar launch.");

            var released = new Vector3[EarthSurfCellGraph.CellCount];
            Assert.That(router.SurfPillarJumpSequence, Is.EqualTo(routeSequence + 1u));
            Assert.That(surf.PillarJumpBreakSequence, Is.EqualTo(breakSequence + 1u));
            Assert.That(raisedCount, Is.EqualTo(1));
            Assert.That(surf.IsActive, Is.False);
            Assert.That(surf.CopyReleasedStonePositionsNonAlloc(released),
                Is.EqualTo(EarthSurfCellGraph.CellCount));
            Assert.That(pillar.IsLaunchPending, Is.True);
            Assert.That(pillar.IsCharging, Is.False);
            Assert.That(Vector3.Angle(up, pillar.LastLaunchDirection), Is.InRange(18f, 28.1f));
            Assert.That(Vector3.Dot(pillar.LastLaunchDirection, travel), Is.GreaterThan(0.25f),
                "The surf pillar and launch must lean toward the actual board travel direction.");
            Assert.That(Vector3.Dot(raisedDirection, pillar.LastLaunchDirection),
                Is.GreaterThan(0.999f));
            GameObject launchPillar = FindByName(scene, "Rising Earth Pillar");
            Assert.That(launchPillar, Is.Not.Null);
            Assert.That(launchPillar.activeSelf, Is.True,
                "The surf trick must raise the existing authored pillar under the rider.");

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var scattered = new Vector3[EarthSurfCellGraph.CellCount];
            Assert.That(surf.CopyReleasedStonePositionsNonAlloc(scattered),
                Is.EqualTo(EarthSurfCellGraph.CellCount));
            int outwardMoving = 0;
            for (int index = 0; index < scattered.Length; index++)
                if (Vector3.Dot(scattered[index] - released[index], pillar.LastLaunchDirection) > 0.01f)
                    outwardMoving++;
            Assert.That(outwardMoving, Is.GreaterThanOrEqualTo(9),
                "Most released board stones must visibly scatter away from the rising pillar.");

            int riseTicks = Mathf.CeilToInt(pillar.LastLaunch.RiseSeconds / Time.fixedDeltaTime) + 3;
            for (int frame = 0; frame < riseTicks; frame++) yield return new WaitForFixedUpdate();
            float rise = Vector3.Dot(body.worldCenterOfMass - start, motor.LocalUp);
            float forwardTravel = Vector3.Dot(body.worldCenterOfMass - start, travel);
            float upSpeed = Vector3.Dot(body.linearVelocity, motor.LocalUp);
            Assert.That(rise, Is.GreaterThan(0.35f));
            Assert.That(forwardTravel, Is.GreaterThan(0.35f),
                "A charged surf pillar must create a real long-jump component, not only a vertical hop.");
            Assert.That(upSpeed, Is.GreaterThan(2.5f));
            Assert.That(Vector3.Dot(launchPillar.transform.up, pillar.LastLaunchDirection),
                Is.GreaterThan(0.98f), "The visible pillar must tilt along the same axis as the rider launch.");

            yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static int CountActiveSemanticCells()
        {
            int count = 0;
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name.StartsWith("Surf Cell ") &&
                    transforms[index].gameObject.activeSelf) count++;
            return count;
        }

        private static int CountAttachedActiveSemanticCells(Transform board)
        {
            int count = 0;
            Transform[] transforms = board.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name.StartsWith("Surf Cell ") &&
                    transforms[index].gameObject.activeSelf) count++;
            return count;
        }
    }
}
