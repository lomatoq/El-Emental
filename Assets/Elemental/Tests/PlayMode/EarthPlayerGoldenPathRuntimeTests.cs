using System.Collections;
using System.Collections.Generic;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Camera;
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
    public sealed class EarthPlayerGoldenPathRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProductionCameraDoesNotCollapseIntoArmorOrReleasedPlates()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            PlanetMotor motor = FindInScene<PlanetMotor>(scene);
            EarthArmorController armor = FindInScene<EarthArmorController>(scene);
            EarthCinemachineCameraController cameraController =
                FindInScene<EarthCinemachineCameraController>(scene);
            UnityEngine.Camera gameplayCamera = FindInScene<UnityEngine.Camera>(scene);
            Assert.That(motor, Is.Not.Null);
            Assert.That(armor, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(gameplayCamera, Is.Not.Null);
            Assert.That(cameraController.IgnoresControlledMagic, Is.True);
            Assert.That(cameraController.HasSphericalClearance, Is.True,
                "The production Cinemachine rig must own a spherical-world final clearance constraint.");

            for (int frame = 0; frame < 20; frame++) yield return null;
            float baseline = Vector3.Distance(gameplayCamera.transform.position, motor.transform.position);
            float minimumDistance = baseline;
            int maximumHiddenArmorPieces = 0;
            Assert.That(armor.Begin(), Is.True);
            for (int frame = 0; frame < 35; frame++)
            {
                cameraController.RefreshArmorVisibilityNow();
                maximumHiddenArmorPieces = Mathf.Max(
                    maximumHiddenArmorPieces,
                    cameraController.HiddenArmorPieceCount);
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }
            for (int step = 0; step < 6; step++) armor.ApplyWheel(120f, Time.unscaledTime);
            for (int frame = 0; frame < 35; frame++)
            {
                cameraController.RefreshArmorVisibilityNow();
                maximumHiddenArmorPieces = Mathf.Max(
                    maximumHiddenArmorPieces,
                    cameraController.HiddenArmorPieceCount);
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }
            // The production shell may naturally leave the protected sightline clear
            // for a particular animation frame. Force one real active plate into the
            // camera-to-head corridor so this test proves the render-only runtime
            // adapter, rather than depending on incidental body-packing phase.
            var activeArmorPieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int activeArmorPieceCount = armor.CopyActivePiecesNonAlloc(activeArmorPieces);
            Assert.That(activeArmorPieceCount, Is.GreaterThan(0));
            EarthArmorPiece sightlinePlate = activeArmorPieces[0];
            Vector3 localUp = motor.LocalUp.sqrMagnitude > 0.5f ? motor.LocalUp.normalized : motor.transform.up;
            Vector3 headFocus = motor.transform.position + localUp * 1.58f;
            sightlinePlate.transform.position = Vector3.Lerp(
                gameplayCamera.transform.position,
                headFocus,
                0.56f);
            UnityEngine.Physics.SyncTransforms();
            cameraController.RefreshArmorVisibilityNow();
            Assert.That(sightlinePlate.CameraSuppressed, Is.True,
                "A real armor renderer between camera and head must yield without disabling its collider.");
            Assert.That(sightlinePlate.PieceCollider.enabled, Is.True);
            Assert.That(sightlinePlate.Body.detectCollisions, Is.True);
            maximumHiddenArmorPieces = Mathf.Max(
                maximumHiddenArmorPieces,
                cameraController.HiddenArmorPieceCount);
            armor.ReleaseAsDebris();
            for (int frame = 0; frame < 20; frame++)
            {
                minimumDistance = Mathf.Min(minimumDistance,
                    Vector3.Distance(gameplayCamera.transform.position, motor.transform.position));
                yield return null;
            }

            Assert.That(minimumDistance, Is.GreaterThan(Mathf.Max(4.5f, baseline * 0.72f)),
                $"Armor collapsed the camera from {baseline:F2} m to {minimumDistance:F2} m.");
            VoxelPlanetBehaviour planet = FindInScene<VoxelPlanetBehaviour>(scene);
            Assert.That(planet, Is.Not.Null);
            float surfaceClearance = Vector3.Distance(
                gameplayCamera.transform.position,
                planet.transform.position) - planet.Radius;
            Assert.That(surfaceClearance, Is.GreaterThanOrEqualTo(0.38f),
                "The long armor camera arm must stay outside the spherical surface instead of being collision-pulled into the hero.");
            Assert.That(maximumHiddenArmorPieces, Is.GreaterThan(0),
                "At least one rear shell plate must yield to the protected head/chest sightline " +
                "instead of covering the gameplay frame.");
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ProductionArmorStartsOffAndCoversEveryVisibleBodyRegion()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            EarthArmorController armor = FindInScene<EarthArmorController>(scene);
            HumanoidCharacterPresentation presentation = FindInScene<HumanoidCharacterPresentation>(scene);
            Assert.That(armor, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(armor.IsActive, Is.False,
                "Armor is an input-owned hold spell and must never assemble on scene startup.");

            for (int frame = 0; frame < 12; frame++) yield return null;
            Animator animator = presentation.Animator;
            SkinnedMeshRenderer visibleBody = FindVisibleHumanoidRenderer(animator);
            Assert.That(visibleBody, Is.Not.Null,
                "The production Humanoid must expose one enabled skinned body renderer.");
            float humanScale = Mathf.Max(0.75f, animator.humanScale);
            Bounds[] bodyRegions =
            {
                GetHumanoidRegionBounds(animator, humanScale * 0.16f,
                    HumanBodyBones.Head, HumanBodyBones.Neck),
                GetHumanoidRegionBounds(animator, humanScale * 0.19f,
                    HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest, HumanBodyBones.Neck),
                GetHumanoidRegionBounds(animator, humanScale * 0.11f,
                    HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                GetHumanoidRegionBounds(animator, humanScale * 0.11f,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                GetHumanoidRegionBounds(animator, humanScale * 0.13f,
                    HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                GetHumanoidRegionBounds(animator, humanScale * 0.13f,
                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)
            };
            string[] labels = { "head", "torso", "left arm", "right arm", "left leg", "right leg" };
            int[] minimumTiles = { 10, 10, 4, 4, 8, 8 };
            for (int region = 0; region < bodyRegions.Length; region++)
                Assert.That(bodyRegions[region].size.sqrMagnitude, Is.GreaterThan(0f),
                    $"Missing valid Humanoid bones for the visible {labels[region]} region.");

            Assert.That(armor.Begin(), Is.True);
            for (int tick = 0; tick < 32; tick++) yield return new WaitForFixedUpdate();
            var pieces = new EarthArmorPiece[EarthArmorProfile.MaximumPieceCount];
            int count = armor.CopyActivePiecesNonAlloc(pieces);
            Assert.That(count, Is.EqualTo(EarthArmorProfile.MaximumPieceCount));
            for (int region = 0; region < bodyRegions.Length; region++)
            {
                Bounds regionBounds = bodyRegions[region];
                float allowance = Mathf.Max(0.18f, regionBounds.extents.magnitude * 0.34f);
                int nearby = 0;
                float farthestFaceGap = 0f;
                for (int piece = 0; piece < count; piece++)
                {
                    if (regionBounds.SqrDistance(pieces[piece].transform.position) <= allowance * allowance)
                        nearby++;
                }

                Vector3[] faceSamples =
                {
                    regionBounds.center + Vector3.right * regionBounds.extents.x,
                    regionBounds.center - Vector3.right * regionBounds.extents.x,
                    regionBounds.center + Vector3.up * regionBounds.extents.y,
                    regionBounds.center - Vector3.up * regionBounds.extents.y,
                    regionBounds.center + Vector3.forward * regionBounds.extents.z,
                    regionBounds.center - Vector3.forward * regionBounds.extents.z
                };
                for (int sample = 0; sample < faceSamples.Length; sample++)
                {
                    float nearest = float.PositiveInfinity;
                    for (int piece = 0; piece < count; piece++)
                        nearest = Mathf.Min(nearest,
                            Vector3.Distance(faceSamples[sample], pieces[piece].transform.position));
                    farthestFaceGap = Mathf.Max(farthestFaceGap, nearest);
                }

                Assert.That(nearby, Is.GreaterThanOrEqualTo(minimumTiles[region]),
                    $"The compact shell leaves the visible {labels[region]} exposed: " +
                    $"only {nearby} fitted tiles near the Humanoid bone region, bounds={regionBounds}.");
                Assert.That(farthestFaceGap, Is.LessThanOrEqualTo(allowance + 0.16f),
                    $"The compact shell has a large uncovered face on the visible {labels[region]} " +
                    $"(gap {farthestFaceGap:F3} m, bounds={regionBounds}).");
            }

            armor.ReleaseAsDebris();
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ProductionMageStandsWalksAndAnimatesWithoutCameraChaos()
        {
            const string scenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            PlanetMotor motor = FindInScene<PlanetMotor>(scene);
            ActiveRagdollPuppet puppet = FindInScene<ActiveRagdollPuppet>(scene);
            HumanoidCharacterPresentation presentation = FindInScene<HumanoidCharacterPresentation>(scene);
            PlanetCameraRig cameraRig = FindInScene<PlanetCameraRig>(scene);
            EarthCinemachineCameraController cameraController =
                FindInScene<EarthCinemachineCameraController>(scene);
            UnityEngine.Camera gameplayCamera = FindInScene<UnityEngine.Camera>(scene);
            Assert.That(motor, Is.Not.Null);
            Assert.That(puppet, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(cameraController.IsLive, Is.True,
                "The production camera must be owned by the local-up Cinemachine rig.");

            Rigidbody body = motor.GetComponent<Rigidbody>();
            Animator animator = presentation.Animator;
            Assert.That(body, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            SkinnedMeshRenderer visibleBody = FindVisibleHumanoidRenderer(animator);
            Assert.That(visibleBody, Is.Not.Null,
                "The visible character must expose an enabled skinned body mesh, not only animated bones.");
            for (int tick = 0; tick < 150; tick++) yield return new WaitForFixedUpdate();

            float idleUpright = Vector3.Dot(body.transform.up, motor.LocalUp);
            Assert.That(motor.enabled, Is.True, "The production motor was disabled during an ordinary idle.");
            Assert.That(puppet.CurrentState.Mode, Is.Not.EqualTo(CharacterPhysicalMode.FullRagdoll),
                "Spawn/support contacts must not put the player into FullRagdoll.");
            Assert.That(idleUpright, Is.GreaterThan(0.9f),
                $"The Mage must visibly stand before accepting input (upright dot {idleUpright:F3}).");
            Assert.That(body.angularVelocity.magnitude, Is.LessThan(1.5f),
                "An idle Mage may not keep tumbling on its support.");

            ScriptedMotorInput scripted = motor.gameObject.AddComponent<ScriptedMotorInput>();
            scripted.Move = new float2(0f, 1f);
            motor.ConfigureInputSource(scripted);
            Vector3 start = body.position;
            Vector3 startUp = motor.LocalUp;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Assert.That(leftFoot, Is.Not.Null);
            Vector3 initialFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
            var leftLegProbe = new VisibleGeometryProbe(visibleBody, animator.transform,
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            var rightLegProbe = new VisibleGeometryProbe(visibleBody, animator.transform,
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.RightFoot));
            float maximumFootTravel = 0f;
            float maximumVisibleLegTravel = 0f;
            float lateCycleVisibleTravel = 0f;
            float minimumUpright = 1f;
            float maximumCameraStep = 0f;
            float maximumCameraAngleStep = 0f;
            float maximumAnimatorSpeed = 0f;
            Vector3 previousCameraPosition = cameraRig.transform.position;
            Quaternion previousCameraRotation = cameraRig.transform.rotation;
            for (int tick = 0; tick < 120; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                minimumUpright = Mathf.Min(minimumUpright, Vector3.Dot(body.transform.up, motor.LocalUp));
                Vector3 footLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                maximumFootTravel = Mathf.Max(maximumFootTravel, Vector3.Distance(initialFootLocal, footLocal));
                maximumVisibleLegTravel = Mathf.Max(maximumVisibleLegTravel,
                    Mathf.Max(leftLegProbe.MeasureMaximumTravel(), rightLegProbe.MeasureMaximumTravel()));
                if (tick == 80)
                {
                    leftLegProbe.CaptureReferencePose();
                    rightLegProbe.CaptureReferencePose();
                }
                else if (tick > 80)
                    lateCycleVisibleTravel = Mathf.Max(
                        lateCycleVisibleTravel,
                        Mathf.Max(
                            leftLegProbe.MeasureMaximumTravelFromReference(),
                            rightLegProbe.MeasureMaximumTravelFromReference()));
                maximumAnimatorSpeed = Mathf.Max(maximumAnimatorSpeed, animator.GetFloat("Speed"));
                maximumCameraStep = Mathf.Max(maximumCameraStep,
                    Vector3.Distance(previousCameraPosition, cameraRig.transform.position));
                maximumCameraAngleStep = Mathf.Max(maximumCameraAngleStep,
                    Quaternion.Angle(previousCameraRotation, cameraRig.transform.rotation));
                previousCameraPosition = cameraRig.transform.position;
                previousCameraRotation = cameraRig.transform.rotation;
                if (puppet.CurrentState.Mode == CharacterPhysicalMode.FullRagdoll)
                    Assert.Fail(
                        $"Ordinary walking entered FullRagdoll at tick {tick}; " +
                        $"balance={puppet.CurrentState.BalanceError:F3}, " +
                        $"debt={puppet.CurrentState.StaggerDebt:F3}, " +
                        $"upright={Vector3.Dot(body.transform.up, motor.LocalUp):F3}, " +
                        $"speed={body.linearVelocity.magnitude:F3}, angular={body.angularVelocity.magnitude:F3}, " +
                        $"contactPoint={puppet.LastCollisionImpact.Point}, " +
                        $"contactNormal={puppet.LastCollisionImpact.Normal}, " +
                        $"contactImpulse={puppet.LastCollisionImpact.Impulse:F3}, " +
                        $"otherDynamic={puppet.LastCollisionImpact.OtherBodyIsDynamic}, " +
                        $"classifiedSupport={puppet.LastCollisionWasSupport}.");
            }

            Vector3 tangentTravel = Vector3.ProjectOnPlane(body.position - start, startUp);
            Assert.That(tangentTravel.magnitude, Is.GreaterThan(3f),
                "A real motor command must move the production Mage; setting Rigidbody velocity in QA is not proof.");
            Assert.That(minimumUpright, Is.GreaterThan(0.94f),
                $"The Mage fell over while walking (minimum upright dot {minimumUpright:F3}).");
            Assert.That(maximumAnimatorSpeed, Is.GreaterThan(1f),
                "Locomotion animation must react while the real motor is moving, even if an obstacle stops it later.");
            Assert.That(maximumFootTravel, Is.GreaterThan(0.05f),
                "The authored locomotion clip must visibly cycle the feet while the motor walks.");
            Assert.That(maximumVisibleLegTravel, Is.GreaterThan(0.035f),
                "The rendered leg geometry must move through the gait; moving only hidden Humanoid bones is not acceptable.");
            Assert.That(lateCycleVisibleTravel, Is.GreaterThan(0.015f),
                "Visible gait froze after its first cycle instead of continuing while the motor walks.");
            AnimatorClipInfo[] activeClips = animator.GetCurrentAnimatorClipInfo(0);
            Assert.That(activeClips.Length, Is.GreaterThan(0));
            for (int index = 0; index < activeClips.Length; index++)
                if (activeClips[index].weight > 0.01f)
                    Assert.That(activeClips[index].clip.isLooping, Is.True,
                        $"Active locomotion clip '{activeClips[index].clip.name}' must loop.");
            AnimatorStateInfo finalLocomotionState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextLocomotionState = animator.GetNextAnimatorStateInfo(0);
            Assert.That(finalLocomotionState.IsName("Locomotion") ||
                        nextLocomotionState.IsName("Locomotion"), Is.True,
                $"Grounded walking must recover to Locomotion, not remain in state " +
                $"{finalLocomotionState.fullPathHash} at {finalLocomotionState.normalizedTime:F2}.");

            scripted.JumpPressed = true;
            yield return new WaitForFixedUpdate();
            scripted.JumpPressed = false;
            bool sawAirborne = false;
            bool relanded = false;
            bool enteredFullRagdollDuringJump = false;
            for (int tick = 0; tick < 180; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                sawAirborne |= !motor.IsGrounded;
                enteredFullRagdollDuringJump |= puppet.CurrentState.Mode == CharacterPhysicalMode.FullRagdoll;
                if (sawAirborne && motor.IsGrounded &&
                    Mathf.Abs(Vector3.Dot(body.linearVelocity, motor.LocalUp)) < 0.6f)
                {
                    relanded = true;
                    break;
                }
            }
            Assert.That(sawAirborne, Is.True, "The golden-path jump never left its support.");
            Assert.That(relanded, Is.True, "The golden-path jump did not return to stable support.");
            Assert.That(enteredFullRagdollDuringJump, Is.False,
                "An ordinary self-powered jump must not disable locomotion through FullRagdoll.");

            for (int tick = 0; tick < 32; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            leftLegProbe.CaptureReferencePose();
            rightLegProbe.CaptureReferencePose();
            float postLandingGaitTravel = 0f;
            for (int tick = 0; tick < 80; tick++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                postLandingGaitTravel = Mathf.Max(postLandingGaitTravel,
                    Mathf.Max(
                        leftLegProbe.MeasureMaximumTravelFromReference(),
                        rightLegProbe.MeasureMaximumTravelFromReference()));
            }
            AnimatorStateInfo postLandingState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo postLandingNext = animator.GetNextAnimatorStateInfo(0);
            AnimatorClipInfo[] postLandingClips = animator.GetCurrentAnimatorClipInfo(0);
            string postLandingClipSummary = postLandingClips.Length == 0
                ? "none"
                : string.Join(", ", System.Array.ConvertAll(postLandingClips,
                    value => $"{value.clip.name}:w={value.weight:F2}:loop={value.clip.isLooping}:len={value.clip.length:F2}"));
            Assert.That(postLandingGaitTravel, Is.GreaterThan(0.015f),
                $"Leg animation must keep cycling after landing. animatorEnabled={animator.enabled}, " +
                $"animatorSpeed={animator.speed:F2}, grounded={motor.IsGrounded}, " +
                $"physicalMode={puppet.CurrentState.Mode}, state={postLandingState.fullPathHash}, " +
                $"time={postLandingState.normalizedTime:F2}, next={postLandingNext.fullPathHash}, " +
                $"clips=[{postLandingClipSummary}].");
            Assert.That(postLandingState.IsName("Locomotion") ||
                        postLandingNext.IsName("Locomotion"), Is.True,
                "A completed landing must return the base layer to Locomotion.");
            leftLegProbe.Dispose();
            rightLegProbe.Dispose();
            Vector3 chestViewport = gameplayCamera.WorldToViewportPoint(body.position + motor.LocalUp * 0.92f);
            Vector3 chestCameraLocal = gameplayCamera.transform.InverseTransformPoint(
                body.position + motor.LocalUp * 0.92f);
            Assert.That(chestViewport.z, Is.GreaterThan(0f));
            Assert.That(chestViewport.y, Is.InRange(0.2f, 0.5f),
                $"The player must sit in the lower third while the ground ahead remains visible " +
                $"(viewport {chestViewport}, camera-local {chestCameraLocal}, " +
                $"camera {gameplayCamera.transform.position}, body {body.position}, " +
                $"pitchDot {-Vector3.Dot(gameplayCamera.transform.forward, motor.LocalUp):F3}, " +
                $"upDot {Vector3.Dot(gameplayCamera.transform.up, motor.LocalUp):F3}, " +
                $"aimPitch {cameraController.AimPitch:F2}).");
            Assert.That(-Vector3.Dot(gameplayCamera.transform.forward, motor.LocalUp), Is.InRange(0.1f, 0.58f),
                $"The gameplay camera must have a bounded downward pitch instead of flying level or diving vertically. " +
                $"cameraForward={gameplayCamera.transform.forward}, localUp={motor.LocalUp}, " +
                $"aimForward={cameraController.AimPivot.forward}, aimPitch={cameraController.AimPitch:F2}, " +
                $"camera={gameplayCamera.transform.position}, body={body.position}.");
            Assert.That(Vector3.Dot(gameplayCamera.transform.up, motor.LocalUp), Is.GreaterThan(0.8f),
                "Cinemachine world-up must follow the spherical surface without rolling the horizon.");
            Assert.That(maximumCameraStep, Is.LessThan(0.75f),
                $"Camera translated {maximumCameraStep:F3} m in one rendered frame.");
            Assert.That(maximumCameraAngleStep, Is.LessThan(8f),
                $"Camera rotated {maximumCameraAngleStep:F2} degrees in one rendered frame.");

            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private sealed class ScriptedMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            public bool JumpPressed;
            public PlanetMotorCommand SampleCommand(uint tick) => new PlanetMotorCommand(tick, Move, JumpPressed);
        }

        private sealed class VisibleGeometryProbe
        {
            private const int MaximumSamples = 64;
            private readonly Renderer _renderer;
            private readonly SkinnedMeshRenderer _skinned;
            private readonly Transform _reference;
            private readonly Mesh _baked;
            private readonly List<Vector3> _vertices = new List<Vector3>(256);
            private readonly int[] _sampleIndices;
            private readonly Vector3[] _initialPositions;
            private readonly Vector3[] _referencePositions;
            private readonly Vector3 _initialBoundsCenter;

            public VisibleGeometryProbe(Renderer renderer, Transform reference, params Transform[] anchors)
            {
                _renderer = renderer;
                _skinned = renderer as SkinnedMeshRenderer;
                _reference = reference;
                _initialBoundsCenter = reference.InverseTransformPoint(renderer.bounds.center);
                if (_skinned == null)
                {
                    _sampleIndices = System.Array.Empty<int>();
                    _initialPositions = System.Array.Empty<Vector3>();
                    _referencePositions = System.Array.Empty<Vector3>();
                    return;
                }

                _baked = new Mesh { name = $"{renderer.name} Golden Path Probe" };
                _skinned.BakeMesh(_baked);
                _baked.GetVertices(_vertices);
                var rankedVertices = new List<VertexCandidate>(_vertices.Count);
                for (int vertex = 0; vertex < _vertices.Count; vertex++)
                {
                    Vector3 referenceLocal = ToReferenceLocal(_vertices[vertex]);
                    float nearest = float.PositiveInfinity;
                    for (int anchor = 0; anchor < anchors.Length; anchor++)
                    {
                        if (anchors[anchor] == null) continue;
                        Vector3 anchorLocal = reference.InverseTransformPoint(anchors[anchor].position);
                        nearest = Mathf.Min(nearest, (referenceLocal - anchorLocal).sqrMagnitude);
                    }
                    rankedVertices.Add(new VertexCandidate(vertex, nearest));
                }
                rankedVertices.Sort((left, right) => left.Distance.CompareTo(right.Distance));
                int sampleCount = Mathf.Min(MaximumSamples, rankedVertices.Count);
                _sampleIndices = new int[sampleCount];
                _initialPositions = new Vector3[sampleCount];
                _referencePositions = new Vector3[sampleCount];
                for (int sample = 0; sample < sampleCount; sample++)
                {
                    int index = rankedVertices[sample].Index;
                    _sampleIndices[sample] = index;
                    _initialPositions[sample] = ToReferenceLocal(_vertices[index]);
                    _referencePositions[sample] = _initialPositions[sample];
                }
            }

            public float MeasureMaximumTravel()
            {
                if (_skinned == null)
                {
                    Vector3 current = _reference.InverseTransformPoint(_renderer.bounds.center);
                    return Vector3.Distance(_initialBoundsCenter, current);
                }

                RefreshSkinnedGeometry();
                float maximum = 0f;
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    maximum = Mathf.Max(maximum,
                        Vector3.Distance(_initialPositions[sample], ToReferenceLocal(_vertices[index])));
                }
                return maximum;
            }

            public void CaptureReferencePose()
            {
                if (_skinned == null) return;
                RefreshSkinnedGeometry();
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    _referencePositions[sample] = ToReferenceLocal(_vertices[index]);
                }
            }

            public float MeasureMaximumTravelFromReference()
            {
                if (_skinned == null) return 0f;
                RefreshSkinnedGeometry();
                float maximum = 0f;
                for (int sample = 0; sample < _sampleIndices.Length; sample++)
                {
                    int index = _sampleIndices[sample];
                    if (index < 0 || index >= _vertices.Count) continue;
                    maximum = Mathf.Max(maximum,
                        Vector3.Distance(_referencePositions[sample], ToReferenceLocal(_vertices[index])));
                }
                return maximum;
            }

            public void Dispose()
            {
                if (_baked != null) Object.Destroy(_baked);
            }

            private Vector3 ToReferenceLocal(Vector3 rendererLocal) =>
                _reference.InverseTransformPoint(_renderer.transform.TransformPoint(rendererLocal));

            private void RefreshSkinnedGeometry()
            {
                _skinned.BakeMesh(_baked);
                _vertices.Clear();
                _baked.GetVertices(_vertices);
            }

            private readonly struct VertexCandidate
            {
                public readonly int Index;
                public readonly float Distance;

                public VertexCandidate(int index, float distance)
                {
                    Index = index;
                    Distance = distance;
                }
            }
        }

        private static SkinnedMeshRenderer FindVisibleHumanoidRenderer(Animator animator)
        {
            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            SkinnedMeshRenderer best = null;
            int bestVertices = -1;
            for (int index = 0; index < renderers.Length; index++)
            {
                SkinnedMeshRenderer candidate = renderers[index];
                if (!candidate.enabled || candidate.sharedMesh == null) continue;
                int vertices = candidate.sharedMesh.vertexCount;
                if (vertices <= bestVertices) continue;
                best = candidate;
                bestVertices = vertices;
            }
            return best;
        }

        private static Bounds GetHumanoidRegionBounds(
            Animator animator,
            float padding,
            params HumanBodyBones[] bones)
        {
            Bounds bounds = default;
            bool initialized = false;
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = animator.GetBoneTransform(bones[index]);
                if (bone == null) continue;
                if (!initialized)
                {
                    bounds = new Bounds(bone.position, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(bone.position);
            }
            if (initialized) bounds.Expand(padding * 2f);
            return bounds;
        }

        private static Renderer FindRenderer(Renderer[] renderers, params string[] tokens)
        {
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                string normalized = renderer.name.Replace("_", string.Empty).ToLowerInvariant();
                for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    string token = tokens[tokenIndex].Replace("_", string.Empty).ToLowerInvariant();
                    if (normalized.Contains(token)) return renderer;
                }
            }
            return null;
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
    }
}
