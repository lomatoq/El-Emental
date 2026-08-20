using Elemental.Simulation.Characters;
using Elemental.Simulation.Matter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Elemental.Tests.EditMode
{
    public sealed class EarthCameraDirectorTests
    {
        [Test]
        public void RequestSolver_HeavyProjectileFramesActionWithoutHardSnapPriorityLoss()
        {
            var envelope = new EarthCameraEnvelope(new float3(2f, 3f, 4f), new float3(1.5f, 0.8f, 0.7f));
            var projectile = new EarthCameraRequest(
                EarthCameraIntent.Projectile, new float3(0f, 0f, 1f), in envelope,
                1200f, 0f, 1f, 0.2f, new EarthMatterId(7, 1), 130);
            EarthCameraRequestResponse response = EarthCameraRequestSolver.Solve(in projectile);

            Assert.That(projectile.IsValid, Is.True);
            Assert.That(response.FocusWeight, Is.GreaterThan(0.7f));
            Assert.That(response.LookAhead, Is.GreaterThan(2f));
            Assert.That(EarthCameraRequestSolver.ShouldReplace(default, 0f, in projectile, 1f), Is.True);

            var lowPriority = new EarthCameraRequest(
                EarthCameraIntent.Aim, float3.zero, in envelope,
                0f, 1f, 0f, 0f, default, 20);
            Assert.That(EarthCameraRequestSolver.ShouldReplace(in projectile, 3f, in lowPriority, 1.2f), Is.False);
        }
        [Test]
        public void StatePriorityKeepsImpactAndAirborneReadable()
        {
            var all = new EarthCameraContext(true, true, true, true, true, true, true, 1f);
            Assert.That(EarthCameraStateResolver.Resolve(in all), Is.EqualTo(EarthCameraState.Impact));

            var airborne = new EarthCameraContext(true, true, true, true, true, false, false, 1f);
            Assert.That(EarthCameraStateResolver.Resolve(in airborne), Is.EqualTo(EarthCameraState.Airborne));

            var heavy = new EarthCameraContext(true, true, false, false, false, false, false, 0.9f);
            Assert.That(EarthCameraStateResolver.Resolve(in heavy), Is.EqualTo(EarthCameraState.BendHeavy));
        }

        [Test]
        public void WeightedFocusCannotAbandonPlayerComposition()
        {
            var input = new EarthCameraFocusInput(
                float3.zero,
                new float3(0f, 0f, 100f),
                new float3(30f, 0f, 0f),
                new float3(-50f, 0f, 0f),
                1f, 1f, 1f, 1f);

            float3 focus = EarthCameraFocusSolver.Solve(in input, 7.5f);

            Assert.That(math.length(focus), Is.LessThanOrEqualTo(7.501f));
            Assert.That(math.all(math.isfinite(focus)), Is.True);
        }

        [Test]
        public void PointerIntentHasSoftDeadZoneAndBoundedNonlinearExtremes()
        {
            EarthCameraPointerIntent centered = EarthCameraPointerIntentSolver.Solve(
                new float2(0.56f, 0.44f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);
            EarthCameraPointerIntent upper = EarthCameraPointerIntentSolver.Solve(
                new float2(0.5f, 1f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);
            EarthCameraPointerIntent lower = EarthCameraPointerIntentSolver.Solve(
                new float2(0.5f, 0f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);
            EarthCameraPointerIntent rightMid = EarthCameraPointerIntentSolver.Solve(
                new float2(0.82f, 0.5f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);

            Assert.That(centered.HorizontalBias, Is.Zero);
            Assert.That(centered.VerticalBias, Is.Zero);
            Assert.That(upper.VerticalBias, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(lower.VerticalBias, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(upper.GroundFocusDistance, Is.GreaterThan(lower.GroundFocusDistance));
            Assert.That(upper.AimElevation, Is.GreaterThan(lower.AimElevation));
            Assert.That(rightMid.HorizontalBias, Is.InRange(0.01f, 0.8f));
        }

        [Test]
        public void FreeCameraStatesIgnoreRawPointerWhileCastingStatesOptIn()
        {
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.Explore), Is.Zero);
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.Airborne), Is.Zero);
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.Impact), Is.Zero);
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.Recovery), Is.Zero);
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.Aim), Is.InRange(0.6f, 0.8f));
            Assert.That(EarthCameraPointerInfluenceSolver.Resolve(EarthCameraState.DrawStructure), Is.EqualTo(1f));
        }

        [Test]
        public void ArmorVisibilitySuppressesOnlyPlatesInsideProtectedSightline()
        {
            float3 camera = new float3(0f, 1f, -7f);
            float3 focus = new float3(0f, 1f, 0f);

            Assert.That(EarthCameraArmorVisibilitySolver.ShouldSuppress(
                camera, focus, new float3(0.12f, 1f, -1f), 0.24f, 0.22f, false), Is.True);
            Assert.That(EarthCameraArmorVisibilitySolver.ShouldSuppress(
                camera, focus, new float3(1.4f, 1f, -1f), 0.24f, 0.22f, false), Is.False);
            Assert.That(EarthCameraArmorVisibilitySolver.ShouldSuppress(
                camera, focus, new float3(0f, 1f, 0.4f), 0.24f, 0.22f, false), Is.False,
                "Front armor beyond the protected avatar focus must remain visible.");
            Assert.That(EarthCameraArmorVisibilitySolver.ShouldSuppress(
                camera, focus, new float3(0.50f, 1f, -1f), 0.24f, 0.22f, true), Is.True,
                "The release margin prevents one-frame visibility flicker on chipped plate edges.");
        }

        [Test]
        public void CompactArmorKeepsCameraOutsideAndPreservesFaceChestAperture()
        {
            float normalDistance = EarthCameraArmorVisibilitySolver.ResolveCameraDistance(6.8f, false, 0f);
            float compactDistance = EarthCameraArmorVisibilitySolver.ResolveCameraDistance(6.8f, true, 0.15f);
            float domeDistance = EarthCameraArmorVisibilitySolver.ResolveCameraDistance(6.8f, true, 0.72f);
            Assert.That(compactDistance, Is.GreaterThanOrEqualTo(7.45f));
            Assert.That(domeDistance, Is.GreaterThan(compactDistance));
            Assert.That(normalDistance, Is.EqualTo(6.8f).Within(0.001f));

            float faceCenter = EarthCameraArmorVisibilitySolver.ResolveBodyCoverageScale(
                true, new float3(0f, 0f, 1f));
            float headSide = EarthCameraArmorVisibilitySolver.ResolveBodyCoverageScale(
                true, new float3(1f, 0f, 0.1f));
            float limb = EarthCameraArmorVisibilitySolver.ResolveBodyCoverageScale(
                false, new float3(0f, 0f, 1f));
            Assert.That(faceCenter, Is.LessThan(0.45f));
            Assert.That(headSide, Is.GreaterThan(0.9f));
            Assert.That(limb, Is.EqualTo(1f));
        }

        [Test]
        public void SphericalClearanceKeepsLongCameraChordOutsidePlanetAndHero()
        {
            float3 planetCenter = float3.zero;
            float3 heroFocus = new float3(0f, 25.05f, 0f);
            float3 desired = new float3(0f, 23.4f, -9.5f);

            float3 resolved = EarthCameraClearanceSolver.Resolve(
                desired,
                planetCenter,
                24f,
                0.42f,
                heroFocus,
                1.35f,
                new float3(0f, 0f, -1f));

            Assert.That(math.distance(resolved, planetCenter), Is.GreaterThanOrEqualTo(24.419f),
                "A long tangent camera chord may never be resolved below the planet shell.");
            Assert.That(math.distance(resolved, heroFocus), Is.GreaterThanOrEqualTo(1.349f));
            Assert.That(math.all(math.isfinite(resolved)), Is.True);
        }

        [Test]
        public void SphericalClearanceLeavesAlreadySafeCameraUnchanged()
        {
            float3 desired = new float3(0f, 27f, -7f);
            float3 resolved = EarthCameraClearanceSolver.Resolve(
                desired,
                float3.zero,
                24f,
                0.42f,
                new float3(0f, 25f, 0f),
                1.35f,
                new float3(0f, 0f, -1f));
            Assert.That(math.distance(resolved, desired), Is.LessThan(0.0001f));
        }

        [Test]
        public void OcclusionPullsInQuicklyAndReleasesOnlyAfterHysteresis()
        {
            var state = new EarthCameraOcclusionState(7f, 0f);
            state = EarthCameraOcclusionSolver.Step(in state, 7f, 2f, true, 0.1f, 24f, 4f, 0.15f);
            Assert.That(state.Distance, Is.EqualTo(4.6f).Within(0.001f));

            EarthCameraOcclusionState waiting = EarthCameraOcclusionSolver.Step(
                in state, 7f, 7f, false, 0.1f, 24f, 4f, 0.15f);
            Assert.That(waiting.Distance, Is.EqualTo(state.Distance).Within(0.001f));

            EarthCameraOcclusionState releasing = EarthCameraOcclusionSolver.Step(
                in waiting, 7f, 7f, false, 0.1f, 24f, 4f, 0.15f);
            Assert.That(releasing.Distance, Is.GreaterThan(waiting.Distance));
            Assert.That(releasing.Distance - waiting.Distance, Is.LessThanOrEqualTo(0.401f));
        }

        [Test]
        public void ReducedMotionSuppressesFovAndStrongShake()
        {
            var full = new EarthCameraAccessibilitySettings(1f, 0.8f, 1f, false);
            var reduced = new EarthCameraAccessibilitySettings(1f, 0.8f, 1f, true);

            Assert.That(reduced.EffectiveShake, Is.LessThan(full.EffectiveShake));
            Assert.That(reduced.EffectiveLag, Is.LessThan(full.EffectiveLag));
            Assert.That(reduced.EffectiveFieldOfViewMotion, Is.Zero);
        }

        [Test]
        public void ShoulderSwapIsDeterministicAndDoesNotDrift()
        {
            float sign = EarthCameraShoulderSolver.Resolve(0f, false);
            Assert.That(sign, Is.EqualTo(1f));
            sign = EarthCameraShoulderSolver.Resolve(sign, true);
            Assert.That(sign, Is.EqualTo(-1f));
            Assert.That(EarthCameraShoulderSolver.Resolve(sign, false), Is.EqualTo(-1f));
            Assert.That(EarthCameraShoulderSolver.Resolve(sign, true), Is.EqualTo(1f));
        }

        [Test]
        public void CameraPureHotLoopIsDeterministicAndAllocationFree()
        {
            var context = new EarthCameraContext(true, true, false, true, false, false, false, 0.82f);
            var focusInput = new EarthCameraFocusInput(
                new float3(0f, 24f, 0f), new float3(0f, 23f, 8f),
                new float3(2f, 25f, 4f), new float3(-2f, 24f, 6f),
                1f, 0.8f, 1.1f, 0.2f);
            EarthCameraOcclusionState occlusion = new EarthCameraOcclusionState(6f, 0f);
            EarthCameraStateResolver.Resolve(in context);
            EarthCameraFocusSolver.Solve(in focusInput, 7.5f);
            EarthCameraPointerIntentSolver.Solve(
                new float2(0.82f, 0.3f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);
            occlusion = EarthCameraOcclusionSolver.Step(
                in occlusion, 6f, 3f, true, 0.016f, 24f, 4.5f, 0.12f);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            float digest = 0f;
            for (int index = 0; index < 4096; index++)
            {
                EarthCameraState state = EarthCameraStateResolver.Resolve(in context);
                float3 focus = EarthCameraFocusSolver.Solve(in focusInput, 7.5f);
                EarthCameraPointerIntent pointer = EarthCameraPointerIntentSolver.Solve(
                    new float2(0.82f, 0.3f), new float2(0.2f, 0.18f), 2.6f, 7.4f, -0.65f, 2.35f);
                bool hit = (index & 7) < 3;
                occlusion = EarthCameraOcclusionSolver.Step(
                    in occlusion, 6f, 3f, hit, 0.016f, 24f, 4.5f, 0.12f);
                digest += (float)state + focus.z + occlusion.Distance + pointer.GroundFocusDistance;
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            Assert.That(float.IsFinite(digest), Is.True);
            Assert.That(digest, Is.GreaterThan(1000f));
        }

        [Test]
        public void AuthoredSceneDoesNotDependOnCameraMigrationCode()
        {
            string rigSource = System.IO.File.ReadAllText(
                "Assets/Elemental/Presentation/Camera/PlanetCameraRig.cs");
            string scene = System.IO.File.ReadAllText(
                "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity");

            StringAssert.DoesNotContain("AddComponent<EarthCameraDirector>", rigSource);
            StringAssert.Contains("Elemental.Presentation.Camera.EarthCameraDirector", scene);
            StringAssert.Contains("guid: 8a59c4a55b814704abea0ee5f95ddc46", scene);
        }
    }
}
