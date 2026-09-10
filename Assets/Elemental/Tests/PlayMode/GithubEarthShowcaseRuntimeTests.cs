using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    // Saved actors, pools, terrain, material and gameplay ingress. These clips prove
    // runtime outcomes, not every physical keyboard chord; input tests cover that.
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        private MagicInputController earthShowInput;
        private MagicExecutor earthShowExecutor;
        private PlanetMotor earthShowMotor;
        private EarthShowcaseCamera earthShowCamera;
        private string earthShowFolder;
        private int earthShowFrame;
        private float earthShowCaptureDelta;
        private readonly List<string> earthShowOutcomes = new();
        private bool earthShowPassed;

        private IEnumerator ReadyEarthShowcase(string name)
        {
            double readyDeadline = Time.realtimeSinceStartupAsDouble + 140;
            while (!flow.IsWorldReady && Time.realtimeSinceStartupAsDouble < readyDeadline) yield return null;
            Assert.That(flow.IsWorldReady, Is.True);
            Assert.That(flow.BeginBot(), Is.True);
            // This corpus tests Earth. Fire muzzle availability is not its combat
            // gate, and the rival must be idle before waiting for the countdown.
            foreach (var bot in All<EarthMvpBotController>()) bot.enabled = false;
            while (flow.State != Elemental.Presentation.UI.FrontendState.Combat && Time.realtimeSinceStartupAsDouble < readyDeadline) yield return null;
            Assert.That(duel.CombatAllowed && duel.HasSimulationAuthority, Is.True);
            Assert.That(duel.PlayerPhase, Is.EqualTo(Elemental.Simulation.Combat.EarthDuelFighterPhase.Active));
            earthShowInput = duel.PlayerTransform.GetComponent<MagicInputController>();
            Assert.That(earthShowInput, Is.Not.Null);
            earthShowInput.SelectElement(ElementId.Earth);
            earthShowExecutor = earthShowInput.EarthExecutor;
            earthShowMotor = duel.PlayerTransform.GetComponent<PlanetMotor>();
            Assert.That(earthShowExecutor, Is.Not.Null);
            // Explicit semantic driver replaces device reads only. Keep motor,
            // authored animation, mana/duel, support and matter gates intact.
            earthShowInput.enabled = false;
            var router = earthShowInput.GetComponent<EarthActionRouterBehaviour>();
            if (router != null) router.enabled = false;
            earthShowCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            earthShowFolder = "BuildReports/Showcase/earth-" + name + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            Directory.CreateDirectory(earthShowFolder);
            earthShowFrame = 0; earthShowPassed = false; earthShowOutcomes.Clear();
            var camera = earthShowInput.CastCamera;
            Assert.That(camera, Is.Not.Null);
            earthShowCamera = camera.gameObject.AddComponent<EarthShowcaseCamera>();
            earthShowCamera.Bind(earthShowMotor);
            yield return EarthShowFrames(.75f);
            Assert.That(earthShowMotor.HasStableSupport, Is.True, "Saved spawn must have real support; do not invent or relocate a floor for footage.");
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthStoneAndHeldFracture()
        {
            yield return ReadyEarthShowcase("stone-fracture");
            float2 point = EarthShowGroundPoint();
            Assert.That(earthShowInput.TryQuickStoneTapAtScreenPoint(point), Is.True, "Quick stone prime rejected");
            yield return EarthShowFrames(.7f);
            Assert.That(earthShowInput.TryQuickStoneTapAtScreenPoint(point + new float2(0, 70)), Is.True, "Quick stone release rejected");
            EarthShowOutcome("Earth02 quick prime and launch admitted");
            yield return EarthShowFrames(.8f);
            point = EarthShowGroundPoint();
            Assert.That(earthShowInput.TryBeginEarthBendAtScreenPoint(point, BendOriginMode.Aim, .95f), Is.True);
            yield return EarthShowFrames(1.7f, () => earthShowInput.TrySetEarthBendTargetAtScreenPoint(point + new float2(0, 55), 1f / 60));
            Assert.That(earthShowExecutor.HeldFragment, Is.Not.Null, "Real extraction must finish before fracturing");
            EarthShowOutcome("Earth01 formed and held an actual arena stone");
            Assert.That(earthShowExecutor.TryFractureHeldBoulder(), Is.True);
            Assert.That(earthShowExecutor.HeldFractureClusterCount, Is.GreaterThan(1));
            yield return EarthShowFrames(1f);
            Vector3 aim = earthShowMotor.FacingForward + earthShowMotor.LocalUp * .22f;
            Assert.That(earthShowExecutor.BeginHeldFractureThrow(aim), Is.True);
            yield return EarthShowFrames(1.15f, () => earthShowExecutor.UpdateHeldFractureThrow(aim));
            int thrown = earthShowExecutor.ReleaseHeldFractureThrow(aim);
            Assert.That(thrown, Is.GreaterThan(1));
            EarthShowOutcome("Earth07 held boulder fractured and charged chunks launched=" + thrown);
            yield return EarthShowFrames(1.4f); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthWallPlatformAndRepair()
        {
            yield return ReadyEarthShowcase("construction-repair");
            var contour = EarthShowContour();
            Assert.That(earthShowInput.SelectEarthAbility(EarthAbilityIds.LineWall), Is.True);
            var line = new[] { contour[0], contour[1], contour[2] };
            earthShowInput.TryPreviewScreenPath(line, .8f); yield return EarthShowFrames(.4f);
            int before = earthShowExecutor.SuccessfulCommandCount;
            Assert.That(earthShowInput.TryCommitScreenPath(line, .8f), Is.True);
            Assert.That(earthShowExecutor.SuccessfulCommandCount, Is.GreaterThan(before));
            var wall = earthShowExecutor.WallPool.LastAcquired;
            Assert.That(wall, Is.Not.Null); yield return EarthShowFrames(1.2f);
            EarthShowOutcome("Earth03 real supported wall raised");
            Assert.That(earthShowExecutor.TryBeginGravityWell(wall.SurfaceCollider, wall.transform.position, earthShowMotor.LocalUp, true), Is.True);
            yield return EarthShowFrames(2.2f, () => earthShowExecutor.SetGravityStructureGesture(EarthGravityStructureIntent.Disassemble, 1));
            Assert.That(wall.IsCollapsing, Is.True, "Unweave must physically break the constructed wall");
            earthShowExecutor.CancelGravityWell();
            Assert.That(wall.Reassembly, Is.Not.Null);
            Assert.That(wall.Reassembly.TryBeginRepair((uint)Time.frameCount), Is.True);
            yield return EarthShowFrames(3.5f);
            Assert.That(wall.Reassembly.WeldedPieceCount, Is.GreaterThan(0));
            EarthShowOutcome("Earth08 unweave and physical repair welded=" + wall.Reassembly.WeldedPieceCount);
            contour = EarthShowContour();
            Assert.That(earthShowInput.SelectEarthAbility(EarthAbilityIds.RaisePlatform), Is.True);
            before = earthShowExecutor.SuccessfulCommandCount;
            Assert.That(earthShowInput.TryCommitScreenPath(contour, 1.1f), Is.True);
            Assert.That(earthShowExecutor.SuccessfulCommandCount, Is.GreaterThan(before));
            Assert.That(earthShowExecutor.PlatformPool.LastAcquired, Is.Not.Null);
            yield return EarthShowFrames(1.8f); EarthShowOutcome("Earth04 supported closed contour platform"); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthGravityClusterAndPush()
        {
            yield return ReadyEarthShowcase("gravity-push");
            // Release genuine cells from a nearby shipping structure through its
            // authored pluck API; no invented meshes, no floor or collision bypass.
            EarthArenaStructure chosen = null; float nearest = 10f;
            foreach (var structure in All<EarthArenaStructure>())
            {
                float distance = Vector3.Distance(structure.transform.position, earthShowMotor.Body.position);
                if (structure.OrdinaryDamageEnabled && structure.Repairable && distance < nearest)
                { chosen = structure; nearest = distance; }
            }
            Assert.That(chosen, Is.Not.Null, "No actual destructible structure within10m");
            IEarthPhysicalTarget first = null;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(chosen.TryPluckCell(chosen.transform.position, out var piece), Is.True);
                if (first == null) first = piece;
            }
            Physics.SyncTransforms();
            Vector3 focus = first.Body.worldCenterOfMass + earthShowMotor.LocalUp * 1.4f;
            Assert.That(earthShowExecutor.TryBeginGravityWell(first.Body.GetComponent<Collider>(), focus, earthShowMotor.LocalUp), Is.True);
            yield return EarthShowFrames(1.7f, () => earthShowExecutor.UpdateGravityWell(focus, earthShowMotor.LocalUp));
            Assert.That(earthShowExecutor.GravityWellCapturedCount, Is.GreaterThan(1));
            EarthShowOutcome("Earth06 gravity held=" + earthShowExecutor.GravityWellCapturedCount);
            Vector3 aim = earthShowMotor.FacingForward + earthShowMotor.LocalUp * .3f;
            Assert.That(earthShowExecutor.BeginGravityClusterThrow(aim), Is.True);
            yield return EarthShowFrames(1.3f, () => earthShowExecutor.UpdateGravityClusterThrow(aim));
            Assert.That(earthShowExecutor.ReleaseGravityClusterThrow(aim), Is.GreaterThan(0));
            EarthShowOutcome("Earth06 compression launched=" + earthShowExecutor.LastGravityLaunchedCount);
            yield return EarthShowFrames(1f);
            Assert.That(chosen.TryPluckCell(chosen.transform.position, out var pushed), Is.True);
            Assert.That(earthShowExecutor.TryBeginVectorField(pushed.Body.GetComponent<Collider>(), pushed.Body, pushed.Body.worldCenterOfMass, aim), Is.True);
            yield return EarthShowFrames(.8f, () => earthShowExecutor.UpdateVectorField(aim, .8f));
            Assert.That(earthShowExecutor.ReleaseVectorField(), Is.True);
            Assert.That(earthShowExecutor.LastMagicPushVelocityChange, Is.GreaterThan(0));
            EarthShowOutcome("Earth05 real vector-field push");
            yield return EarthShowFrames(1.3f); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthArmorMorphShotsAndBurst()
        {
            yield return ReadyEarthShowcase("armor");
            var armor = earthShowInput.GetComponent<EarthArmorController>(); Assert.That(armor, Is.Not.Null);
            Assert.That(armor.Begin(), Is.True); yield return EarthShowFrames(1.4f);
            Assert.That(armor.ActivePieceCount, Is.GreaterThan(1));
            for (int i = 0; i < 7; i++) { armor.ApplyWheel(120, Time.unscaledTime); yield return EarthShowFrames(.2f); }
            Assert.That(armor.Phase01, Is.GreaterThan(.78f));
            EarthShowOutcome("Earth09 compact→dome→orbit actual pieces=" + armor.ActivePieceCount);
            Vector3 target = earthShowMotor.Body.position + earthShowMotor.FacingForward * 20 + earthShowMotor.LocalUp * 2;
            Assert.That(armor.FireNearestAtPoint(target), Is.True); yield return EarthShowFrames(.45f);
            Assert.That(armor.FireAllAtPoint(target), Is.GreaterThan(0));
            EarthShowOutcome("Earth10 nearest and all-plate fire"); yield return EarthShowFrames(1.2f);
            bool restarted = false;
            yield return EarthShowFramesUntil(() => restarted, 2f, () => { if (!restarted) restarted = armor.Begin(); });
            yield return EarthShowFrames(1f);
            for (int i = 0; i < 12 && armor.Phase01 < .999f; i++) armor.ApplyWheel(120, Time.unscaledTime);
            Assert.That(armor.Phase01, Is.GreaterThanOrEqualTo(.999f));
            Assert.That(armor.ApplyWheel(120, Time.unscaledTime), Is.EqualTo(EarthArmorInputResult.OverscrollArmed));
            yield return EarthShowFrames(.1f);
            Assert.That(armor.ApplyWheel(120, Time.unscaledTime), Is.EqualTo(EarthArmorInputResult.RadialRelease));
            armor.ReleaseRadially(); Assert.That(armor.IsActive, Is.False);
            EarthShowOutcome("Earth11 confirmed maximum wheel radial release");
            yield return EarthShowFrames(1.5f); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthPairedSeriesAndPillarCrest()
        {
            yield return ReadyEarthShowcase("combo-crest");
            var dual = earthShowInput.GetComponent<EarthDualMouseAbilityController>(); Assert.That(dual, Is.Not.Null);
            int committed = 0; Action<float> onShot = _ => committed++; dual.StoneShotCommitted += onShot;
            var comboTrace = new System.Text.StringBuilder("time,beat,normalized,active,stone,committed,stunned,grounded,focused,playerPhase,poolAvailable\n");
            void TraceCombo()
            {
                comboTrace.AppendLine($"{Time.time:F3},{dual.CurrentShotBeat},{dual.ComboNormalizedTime:F3},{dual.IsComboActionActive},{dual.IsStompStoneActive},{committed},{earthShowMotor.IsImpactStunned},{earthShowMotor.IsGrounded},{Application.isFocused},{duel.PlayerPhase},{earthShowExecutor.FragmentPool.AvailableCount}");
            }
            try
            {
                for (int beat = 0; beat < 5; beat++)
                {
                    Vector3 shotTarget = earthShowMotor.Body.position + earthShowMotor.FacingForward * 24 + earthShowMotor.LocalUp * 12;
                    Vector3 screen = earthShowInput.CastCamera.WorldToScreenPoint(shotTarget);
                    Assert.That(screen.z, Is.GreaterThan(0));
                    Assert.That(dual.CastStompStone((Vector2)screen), Is.True);
                    Assert.That(dual.CurrentShotBeat, Is.EqualTo((EarthQuickStoneBeat)beat));
                    yield return EarthShowFramesUntil(() => !dual.IsComboActionActive, 2.5f, TraceCombo);
                    TraceCombo();
                    Assert.That(committed, Is.EqualTo(beat + 1));
                }
                EarthShowOutcome("Earth15 all five genuine hand/foot release events=" + committed);
            }
            finally
            {
                dual.StoneShotCommitted -= onShot;
                File.WriteAllText(earthShowFolder + "/combo-trace.csv", comboTrace.ToString());
            }
            var contour = EarthShowContour();
            Vector2 a = (Vector2)contour[0], b = (Vector2)contour[2];
            a /= new Vector2(Screen.width, Screen.height); b /= new Vector2(Screen.width, Screen.height);
            Assert.That(dual.CastPillarCrest(a, b, 5), Is.True);
            int visible = 0; var crestColumns = All<EarthPillarWaveColumn>();
            yield return EarthShowFrames(2f, () =>
            {
                int count = 0; foreach (var column in crestColumns)
                    if (column.TryGetVisiblePlacementDiagnostic(out _, out _, out _, out _, out _, out _)) count++;
                visible = Mathf.Max(visible, count);
            });
            Assert.That(visible, Is.GreaterThan(0)); EarthShowOutcome("Earth16 real crest visible columns=" + visible);
            earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthResonanceAndChargedWave()
        {
            yield return ReadyEarthShowcase("resonance-wave");
            var resonance = earthShowInput.GetComponent<EarthResonanceController>(); Assert.That(resonance, Is.Not.Null);
            var savedResonances = All<EarthResonanceController>();
            Assert.That(savedResonances.Length, Is.EqualTo(2));
            foreach (var configured in savedResonances)
                Assert.That(configured.HasRequiredBindings, Is.True, "Saved resonance must bind its own actor, executor and pool: " + configured.name);
            Vector3 aim = earthShowMotor.FacingForward + earthShowMotor.LocalUp * .15f;
            Assert.That(resonance.BeginCharge(Time.fixedUnscaledTime), Is.True);
            yield return EarthShowFrames(1.7f, () => resonance.ContinueCharge(Time.fixedUnscaledTime, aim));
            Assert.That(resonance.ActiveStoneCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(resonance.ReleaseCharge(Time.fixedUnscaledTime, aim), Is.True);
            yield return EarthShowFrames(.75f);
            Assert.That(resonance.FireNearest(aim, Time.fixedUnscaledTime), Is.True); yield return EarthShowFrames(.3f);
            Assert.That(resonance.FireAll(aim), Is.GreaterThan(0));
            EarthShowOutcome("Earth14 charged hemisphere, individual and all-stone volley");
            yield return EarthShowFrames(1f);
            var wave = earthShowInput.GetComponent<EarthPillarWaveAbility>(); Assert.That(wave, Is.Not.Null);
            Assert.That(wave.BeginCharge(1.5f), Is.True, wave.LastRejection.ToString());
            yield return EarthShowFrames(1.25f); Assert.That(wave.ReleaseCharge(), Is.True, wave.LastRejection.ToString());
            Assert.That(wave.LastColumnCount, Is.GreaterThan(0));
            EarthShowOutcome("Earth13 released charged ground wave columns=" + wave.LastColumnCount);
            yield return EarthShowFrames(2.5f); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthSurfPillarAndLandingSlam()
        {
            yield return ReadyEarthShowcase("movement-slam");
            var surf = earthShowInput.GetComponent<EarthSurfController>(); Assert.That(surf, Is.Not.Null);
            Vector3 start = earthShowMotor.Body.position;
            Assert.That(surf.Begin(Time.unscaledTime, earthShowMotor.FacingForward), Is.True);
            yield return EarthShowFrames(1.1f, () => surf.Continue(Vector2.up, earthShowMotor.FacingForward));
            Assert.That(Vector3.Distance(start, earthShowMotor.Body.position), Is.GreaterThan(1));
            surf.Release(Time.unscaledTime); yield return EarthShowFrames(1f); surf.Cancel();
            EarthShowOutcome("Earth17 actual supported surf travel=" + Vector3.Distance(start, earthShowMotor.Body.position));
            yield return EarthShowFramesUntil(() => earthShowMotor.HasStableSupport, 5f);
            var pillar = earthShowInput.GetComponent<EarthPillarMobility>(); Assert.That(pillar, Is.Not.Null);
            var slam = earthShowInput.GetComponent<EarthLandingSlam>(); Assert.That(slam, Is.Not.Null);
            int impacts = slam.CommitCount;
            Assert.That(pillar.BeginCharge(), Is.True); yield return EarthShowFrames(.9f);
            Assert.That(pillar.ReleaseCharge(), Is.True);
            yield return EarthShowFramesUntil(() => !earthShowMotor.IsGrounded && !pillar.IsLaunchPending, 2f);
            Assert.That(earthShowMotor.IsGrounded, Is.False);
            EarthShowOutcome("Earth12 genuine supported pillar launch");
            slam.SetHeld(true); yield return EarthShowFramesUntil(() => slam.CommitCount > impacts, 7f, () => slam.SetHeld(true));
            Assert.That(slam.CommitCount, Is.GreaterThan(impacts), slam.LastRejection);
            EarthShowOutcome("Earth18 real airborne slam contact columns=" + slam.LastWaveColumnCount);
            slam.SetHeld(false); yield return EarthShowFrames(1.3f); earthShowPassed = true;
        }

        [UnityTest, Timeout(240000)] public IEnumerator ShowcaseEarthIncomingStoneCounter()
        {
            yield return ReadyEarthShowcase("counter");
            var guard = earthShowInput.GetComponent<EarthStoneCounterGuard>(); Assert.That(guard, Is.Not.Null);
            Vector3 forward = earthShowMotor.FacingForward, center = earthShowMotor.Body.worldCenterOfMass;
            var pool = earthShowExecutor.FragmentPool;
            // Only adversarial input is authored here: a shipping pooled stone,
            // with real mass and collision, launched towards the unmodified guard.
            var stone = pool.Acquire(null, center + forward * 3.1f, .3f, pool.ResolveNewStoneMass(4f / 3f * Mathf.PI * .027f));
            Assert.That(stone, Is.Not.Null); Physics.SyncTransforms();
            uint sequence = guard.Sequence; guard.SetHeld(true, forward);
            stone.Body.linearVelocity = -forward * 11f;
            yield return EarthShowFramesUntil(() => guard.Sequence > sequence, 1.3f, () => guard.SetHeld(true, forward));
            Assert.That(guard.Sequence, Is.GreaterThan(sequence), guard.LastRejection);
            EarthShowOutcome("Earth19 real incoming stone partition/counter event=" + guard.Sequence);
            guard.SetHeld(false, forward); yield return EarthShowFrames(1.6f); earthShowPassed = true;
        }

        private void EarthShowOutcome(string text)
        { earthShowOutcomes.Add(text); File.WriteAllLines(earthShowFolder + "/outcomes.txt", earthShowOutcomes); }

        private IEnumerator EarthShowFramesUntil(Func<bool> done, float timeout, Action step = null)
        {
            int budget = Mathf.CeilToInt(timeout * 60);
            for (int i = 0; i < budget && !done(); i++) yield return EarthShowFrames(1f / 60, step);
            Assert.That(done(), Is.True, "Runtime outcome timed out after " + timeout + "s");
        }
        private IEnumerator EarthShowFrames(float seconds, Action step = null)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(seconds * 60));
            for (int i = 0; i < count; i++)
            {
                step?.Invoke(); yield return null;
                if ((earthShowFrame++ % 5) != 0) continue;
                yield return new WaitForEndOfFrame();
                var source = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(source, Is.Not.Null);
                int width = 640, height = Mathf.Max(2, Mathf.RoundToInt(source.height * (640f / source.width)) / 2 * 2);
                var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var previousTarget = RenderTexture.active;
                var output = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    Graphics.Blit(source, target); RenderTexture.active = target;
                    output.ReadPixels(new Rect(0, 0, width, height), 0, 0); output.Apply();
                    File.WriteAllBytes(earthShowFolder + "/" + ((earthShowFrame - 1) / 5).ToString("D05") + ".jpg", output.EncodeToJPG(92));
                }
                finally { RenderTexture.active = previousTarget; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(output); }
            }
        }

        private float2 EarthShowGroundPoint()
        {
            var contour = EarthShowContour(); return contour[0];
        }
        private float2[] EarthShowContour()
        {
            var camera = earthShowInput.CastCamera;
            var profile = earthShowExecutor.PlatformPool.Profile;
            var points = new float2[9]; var world = new float3[9];
            for (int scale = 1; scale <= 3; scale++)
            for (float y = Screen.height * .3f; y < Screen.height * .8f; y += 22)
            for (float x = Screen.width * .2f; x < Screen.width * .8f; x += 22)
            {
                float w = Mathf.Max(28, Screen.width / 44f) * scale, h = Mathf.Max(20, Screen.height / 44f) * scale;
                points[0] = new float2(x - w, y - h); points[1] = new float2(x, y - h); points[2] = new float2(x + w, y - h);
                points[3] = new float2(x + w, y); points[4] = new float2(x + w, y + h); points[5] = new float2(x, y + h);
                points[6] = new float2(x - w, y + h); points[7] = new float2(x - w, y); points[8] = points[0];
                Collider surface = null; bool valid = true;
                for (int i = 0; i < 9; i++)
                {
                    if (!Physics.Raycast(camera.ScreenPointToRay((Vector2)points[i]), out var hit, 150, ~0, QueryTriggerInteraction.Ignore) ||
                        hit.collider.GetComponentInParent<EarthArenaSurfaceProvider>() == null || !hit.collider.name.Contains("FloorBase") ||
                        Vector3.Distance(hit.point, earthShowMotor.Body.position) > 10 || (surface != null && surface != hit.collider)) { valid = false; break; }
                    surface = hit.collider; world[i] = (float3)hit.point;
                }
                if (!valid) continue;
                var geometry = EarthPlatformGeometrySolver.Build(world, (float3)earthShowInput.PlanetCenterWorld);
                if (geometry.IsValid && geometry.Area > profile.MinimumArea * 2 && geometry.Area < profile.MaximumArea * .7f) return points;
            }
            Assert.Fail("No visible genuine arena contour in normal range/area gates"); return null;
        }

        [UnityTearDown] public IEnumerator EarthShowcaseCleanup()
        {
            if (earthShowFolder != null)
            {
                File.WriteAllText(earthShowFolder + "/capture.txt", "fps=12\nwidth=640\npassed=" + earthShowPassed + "\nframes=" + ((earthShowFrame + 4) / 5) + "\nRuntime semantic ingress; no keyboard acceptance or standalone performance claim.\n" + string.Join("\n", earthShowOutcomes));
                Time.captureDeltaTime = earthShowCaptureDelta;
            }
            if (earthShowCamera != null) UnityEngine.Object.Destroy(earthShowCamera);
            earthShowFolder = null; yield return null;
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class EarthShowcaseCamera : MonoBehaviour
    {
        private PlanetMotor actor; private Vector3 forward;
        public void Bind(PlanetMotor value) { actor = value; forward = value.FacingForward; }
        private void LateUpdate()
        {
            if (actor == null) return;
            Vector3 up = actor.LocalUp, tangent = Vector3.ProjectOnPlane(forward, up).normalized;
            Vector3 side = Vector3.Cross(up, tangent);
            Vector3 subject = actor.Body.position + tangent * 2 + up;
            transform.SetPositionAndRotation(actor.Body.position - tangent * 8 + side * 5 + up * 5,
                Quaternion.LookRotation(subject - (actor.Body.position - tangent * 8 + side * 5 + up * 5), up));
        }
    }
}





