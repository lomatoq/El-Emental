using System.Collections;
using Elemental.Presentation.Animation;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthCoreV2FoundationTests
    {
        [UnityTest]
        public IEnumerator WallEmergenceMovesOnlyVisualChildAndActivatesNonPenetratingCollider()
        {
            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.name = "V2 Wall Safety Planet";
            planet.transform.localScale = Vector3.one * 24f;
            SphereCollider planetCollider = planet.GetComponent<SphereCollider>();

            GameObject poolObject = new GameObject("V2 Wall Pool");
            poolObject.SetActive(false);
            EarthWallPool pool = poolObject.AddComponent<EarthWallPool>();
            pool.Configure(1, null, null);
            poolObject.SetActive(true);
            EarthWall wall = pool.Acquire(
                new Vector3(-3f, 12f, 0f),
                new Vector3(3f, 12f, 0f),
                Vector3.zero,
                2.5f,
                0.55f);
            Vector3 rootStart = wall.transform.position;
            Quaternion rotationStart = wall.transform.rotation;
            Assert.That(wall.VisualEmergenceRoot, Is.Not.Null);
            Assert.That(wall.GetComponent<BoxCollider>().enabled, Is.False);

            yield return new WaitForSeconds(0.8f);
            yield return new WaitForFixedUpdate();

            Assert.That(wall.IsEmergenceComplete, Is.True);
            Assert.That(Vector3.Distance(wall.transform.position, rootStart), Is.LessThan(0.02f));
            Assert.That(Quaternion.Angle(wall.transform.rotation, rotationStart), Is.LessThan(0.2f));
            Assert.That(wall.PeakRootEmergenceDisplacementMeters, Is.LessThan(0.02f));
            Assert.That(wall.VisualEmergenceRoot.localPosition.magnitude, Is.LessThan(0.001f));
            Assert.That(wall.VisualEmergenceRoot.localScale, Is.EqualTo(Vector3.one));
            BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
            Assert.That(wallCollider.enabled, Is.True);
            bool penetrating = Physics.ComputePenetration(
                wallCollider, wall.transform.position, wall.transform.rotation,
                planetCollider, planet.transform.position, planet.transform.rotation,
                out _, out float penetration);
            Assert.That(!penetrating || penetration <= 0.012f, Is.True,
                $"Activated wall collider penetrated the planet by {penetration:F4} m.");

            Object.Destroy(poolObject);
            Object.Destroy(planet);
            yield return null;
        }

        [UnityTest]
        public IEnumerator KayKitHumanoidConsumesLocomotionVelocityWithoutRootMotion()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            HumanoidCharacterPresentation presentation = FindInScene<HumanoidCharacterPresentation>(scene);
            Assert.That(presentation, Is.Not.Null);
            Animator animator = presentation.Animator;
            PlanetMotor motor = presentation.GetComponentInParent<PlanetMotor>();
            Rigidbody body = presentation.GetComponentInParent<Rigidbody>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));

            float groundedDeadline = Time.realtimeSinceStartup + 2.5f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < groundedDeadline)
                yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.True,
                "The authored character must settle onto its local support before locomotion is evaluated.");
            yield return new WaitForSeconds(1.05f);

            CapsuleCollider capsule = body.GetComponent<CapsuleCollider>();
            float clearance = MeasureSupportClearance(body, capsule, motor.LocalUp);
            Assert.That(clearance, Is.LessThan(0.2f),
                $"The hidden puppet may support the root capsule, but it must not recreate the old suspension hover ({clearance:F3} m clearance).");

            Vector3 tangent = Vector3.ProjectOnPlane(presentation.transform.forward, motor.LocalUp).normalized;
            if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.Cross(motor.LocalUp, Vector3.right).normalized;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Vector3 firstFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
            float maximumFootTravel = 0f;
            for (int frame = 0; frame < 75; frame++)
            {
                body.linearVelocity = tangent * 3.2f;
                yield return null;
                Vector3 footLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                maximumFootTravel = Mathf.Max(maximumFootTravel, Vector3.Distance(firstFootLocal, footLocal));
            }

            Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0.05f));
            AnimatorStateInfo locomotionState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(locomotionState.IsName("Base Layer.Locomotion") || locomotionState.IsName("Locomotion"), Is.True);
            Assert.That(animator.isHuman, Is.True);
            Assert.That(maximumFootTravel, Is.GreaterThan(0.025f),
                "The locomotion clip must be allowed to move the feet; idle foot IK may not pin walking poses.");

            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            Assert.That(executor, Is.Not.Null);
            GameObject castTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            castTarget.name = "V2 Animation Cast Target";
            castTarget.transform.position = body.worldCenterOfMass + tangent * 4f + motor.LocalUp * 0.4f;
            Rigidbody castBody = castTarget.AddComponent<Rigidbody>();
            castBody.useGravity = false;
            PhysicalImpactTarget castPhysical = castTarget.AddComponent<PhysicalImpactTarget>();
            castPhysical.Configure(castBody);
            Assert.That(executor.TryBeginVectorField(
                castTarget.GetComponent<Collider>(), castBody, castBody.worldCenterOfMass, tangent), Is.True);
            executor.UpdateVectorField(tangent, 1f);
            yield return new WaitForSeconds(0.24f);
            int magicLayer = animator.GetLayerIndex("Earth Magic Upper Body");
            Assert.That(magicLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(animator.GetLayerWeight(magicLayer), Is.GreaterThan(0.75f),
                "The Cast parameter alone is insufficient; the authored upper-body layer must receive visible weight.");
            AnimatorStateInfo magicState = animator.GetCurrentAnimatorStateInfo(magicLayer);
            Assert.That(magicState.IsName("Earth Magic Upper Body.Earth Cast") || magicState.IsName("Earth Cast"), Is.True);
            executor.ReleaseVectorField();
            Object.Destroy(castTarget);

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator PushRaySkipsCasterAndMovesTheWallInsteadOfLaunchingTheMage()
        {
            GameObject cameraObject = new GameObject("V2 Self Push Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject executorObject = new GameObject("V2 Self Push Executor");
            MagicExecutor executor = executorObject.AddComponent<MagicExecutor>();

            GameObject caster = new GameObject("V2 Push Caster");
            caster.SetActive(false);
            caster.transform.position = new Vector3(0f, 0f, 1.5f);
            Rigidbody casterBody = caster.AddComponent<Rigidbody>();
            casterBody.useGravity = false;
            CapsuleCollider casterCollider = caster.AddComponent<CapsuleCollider>();
            PlanetMotor casterMotor = caster.AddComponent<PlanetMotor>();
            casterMotor.enabled = false;
            PhysicalImpactTarget casterImpact = caster.AddComponent<PhysicalImpactTarget>();
            casterImpact.Configure(casterBody);
            PlayerInput playerInput = caster.AddComponent<PlayerInput>();
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap gameplay = actions.AddActionMap("Gameplay");
            gameplay.AddAction("Move", InputActionType.Value);
            gameplay.AddAction("JumpOrStomp", InputActionType.Button);
            gameplay.AddAction("BendPrimary", InputActionType.Button);
            gameplay.AddAction("BendForce", InputActionType.Button);
            gameplay.AddAction("BendField", InputActionType.Button);
            gameplay.AddAction("BendModifier", InputActionType.Button);
            gameplay.AddAction("BendParameter", InputActionType.Value);
            gameplay.AddAction("Cancel", InputActionType.Button);
            gameplay.AddAction("ShoulderSwap", InputActionType.Button);
            gameplay.AddAction("Pointer", InputActionType.Value);
            playerInput.actions = actions;
            playerInput.defaultActionMap = "Gameplay";
            MagicInputController input = caster.AddComponent<MagicInputController>();
            input.Configure(playerInput, camera, executor, null, null);
            caster.SetActive(true);

            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "V2 Push Wall Behind Caster";
            EarthWall wall = wallObject.AddComponent<EarthWall>();
            wall.Initialize(
                71u,
                new Vector3(-1.8f, 0f, 5f),
                new Vector3(1.8f, 0f, 5f),
                new Vector3(0f, -10f, 5f),
                2.5f,
                0.5f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!wall.IsEmergenceComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(wall.IsEmergenceComplete, Is.True);
            Physics.SyncTransforms();

            Assert.That(executor.TryBeginVectorField(
                casterCollider, casterBody, casterBody.worldCenterOfMass, Vector3.forward), Is.False,
                "Executor defense must reject character bodies even when called without input targeting.");
            Vector3 wallStart = wall.transform.position;
            Assert.That(input.TryBeginPushAtScreenPoint(
                new float2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.True);
            Assert.That(executor.VectorFieldBody, Is.SameAs(wall.Body),
                "The target locked through the caster silhouette must be the wall, never the caster Rigidbody.");
            for (int tick = 0; tick < 45; tick++)
            {
                executor.UpdateVectorField(Vector3.forward, 1f);
                yield return new WaitForFixedUpdate();
            }
            executor.ReleaseVectorField();
            yield return new WaitForFixedUpdate();

            Assert.That(Vector3.Distance(wall.transform.position, wallStart), Is.GreaterThan(0.3f),
                "A locked RMB field must translate the aimed wall after skipping every caster collider.");

            Object.Destroy(cameraObject);
            Object.Destroy(executorObject);
            Object.Destroy(caster);
            Object.Destroy(wallObject);
            Object.Destroy(actions);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionCameraRayLocksAndQuicklyShovesVisibleWall()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            Camera camera = FindInScene<Camera>(scene);
            PlanetMotor motor = FindInScene<PlanetMotor>(scene);
            MagicInputController input = FindInScene<MagicInputController>(scene);
            MagicExecutor executor = FindInScene<MagicExecutor>(scene);
            EarthWallPool wallPool = FindInScene<EarthWallPool>(scene);
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Assert.That(camera, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            Assert.That(wallPool, Is.Not.Null);
            Assert.That(planet, Is.Not.Null);

            for (int tick = 0; tick < 90; tick++) yield return new WaitForFixedUpdate();
            Vector3 center = planet.transform.position;
            Vector3 up = motor.LocalUp.normalized;
            Vector3 forward = Vector3.ProjectOnPlane(motor.FacingForward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 radial = (motor.transform.position + forward * 2.8f - right * 0.8f - center).normalized;
            Vector3 midpoint = center + radial * planet.Radius;
            Vector3 start = center + (midpoint - right * 1.65f - center).normalized * planet.Radius;
            Vector3 end = center + (midpoint + right * 1.65f - center).normalized * planet.Radius;
            EarthWall wall = wallPool.Acquire(start, end, center, 2.4f, 0.5f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!wall.IsEmergenceComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(wall.IsEmergenceComplete, Is.True);
            Physics.SyncTransforms();

            Vector3 screen = camera.WorldToScreenPoint(wall.Body.worldCenterOfMass);
            Assert.That(screen.z, Is.GreaterThan(0f), "The production camera must actually see the target wall.");
            Assert.That(screen.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(screen.y, Is.InRange(0f, (float)Screen.height));
            Vector3 casterBefore = motor.transform.position;
            Vector3 wallBefore = wall.transform.position;
            Assert.That(input.TryBeginPushAtScreenPoint(new float2(screen.x, screen.y)), Is.True,
                "A quick RMB press through the production Cinemachine ray must lock the visible wall.");
            Assert.That(executor.VectorFieldBody, Is.SameAs(wall.Body));
            Ray pushRay = camera.ScreenPointToRay(screen);
            executor.UpdateVectorField(pushRay.direction, 0f);
            Assert.That(executor.ReleaseVectorField(), Is.True);
            for (int tick = 0; tick < 14; tick++) yield return new WaitForFixedUpdate();

            float wallTravel = Vector3.Distance(wall.transform.position, wallBefore);
            float casterTravel = Vector3.Distance(motor.transform.position, casterBefore);
            Assert.That(wallTravel, Is.GreaterThan(0.65f),
                $"A quick wall push must be immediately readable (travel {wallTravel:F3} m).");
            Assert.That(casterTravel, Is.LessThan(wallTravel * 0.65f),
                "The spell must move the wall instead of feeding the impulse back into the caster.");

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static float MeasureSupportClearance(
            Rigidbody body,
            CapsuleCollider capsule,
            Vector3 up)
        {
            Vector3 scale = capsule.transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * 0.5f * Mathf.Abs(scale.y));
            Vector3 origin = capsule.transform.TransformPoint(capsule.center);
            RaycastHit[] hits = new RaycastHit[16];
            int count = Physics.RaycastNonAlloc(origin, -up, hits, halfHeight + 3f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                Collider candidate = hits[index].collider;
                if (candidate == null || candidate.attachedRigidbody == body ||
                    candidate.transform.IsChildOf(body.transform)) continue;
                nearest = Mathf.Min(nearest, hits[index].distance);
            }
            return float.IsFinite(nearest) ? Mathf.Max(0f, nearest - halfHeight) : float.PositiveInfinity;
        }


        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T found = roots[index].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
