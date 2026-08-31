using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Elemental.Presentation.Animation;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthAnimationContactTelemetryRuntimeTests
    {
        private const string ScenePath =
            "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const float ScenarioDurationSeconds = 6f;
        private static readonly int[] TargetFrameRates = { 30, 60, 120 };

        [UnityTest]
        public IEnumerator ProductionActorsEmitRealThirtySixtyOneTwentyMatrix()
        {
            int originalVSync = QualitySettings.vSyncCount;
            int originalTargetFrameRate = Application.targetFrameRate;
            float originalCaptureDelta = Time.captureDeltaTime;
            float originalFixedDelta = Time.fixedDeltaTime;
            var report = new ContactMatrixReport
            {
                schema = "animation-contact-v1",
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                scene = ScenePath,
                captureControls =
                    "QualitySettings.vSyncCount=0; Application.targetFrameRate=FPS; " +
                    "Time.captureDeltaTime=1/FPS; Time.fixedDeltaTime=1/60; " +
                    "fresh scene reload per FPS",
                crossFpsComparison =
                    "30/60/120 compares normalized hard-gate outcome/violation rates. " +
                    "Raw magnitudes and sample counts remain serialized per actor/FPS; " +
                    "render-step means are not compared because fixed pose inertializers " +
                    "intentionally apply a per-rendered-pose cap at and below 60 Hz.",
                fpsRuns = new List<FpsRunReport>(),
                frames = new List<ContactFrameRecord>(3000),
                coverage = new List<CoverageRecord>(),
                crossFps = new List<CrossFpsRecord>(),
                limitations = new List<string>()
            };

            QualitySettings.vSyncCount = 0;
            try
            {
                for (int index = 0; index < TargetFrameRates.Length; index++)
                {
                    int fps = TargetFrameRates[index];
                    Application.targetFrameRate = fps;
                    Time.captureDeltaTime = 1f / fps;
                    // Render-FPS is the variable under test; changing physics Hz
                    // moved the bot onto a different part of the curved arena and
                    // invalidated the comparison. Production physics stays at 60.
                    Time.fixedDeltaTime = 1f / 60f;
                    yield return RunProductionFps(fps, report);
                    AddDeterministicEnvironmentBoundaryCoverage(fps, report.coverage);
                }
            }
            finally
            {
                Time.captureDeltaTime = originalCaptureDelta;
                Time.fixedDeltaTime = originalFixedDelta;
                Application.targetFrameRate = originalTargetFrameRate;
                QualitySettings.vSyncCount = originalVSync;
            }

            EvaluateCrossFps(report);
            EvaluateCoverage(report);
            report.metricHardGatesPassed = EveryActorPassesMetricGates(report);
            report.passed = report.metricHardGatesPassed &&
                            report.crossFpsPassed &&
                            report.requestedCoveragePassed;
            WriteReport(report);

            Assert.That(report.metricHardGatesPassed, Is.True,
                "One or more measured player/bot contact gates are red. Read " +
                "BuildReports/AnimationArenaTelemetryLatest.json for the exact actor/FPS metric.");
            Assert.That(report.crossFpsPassed, Is.True,
                "The 30/60/120 outcome delta exceeded 10%; read the crossFps records.");
            Assert.That(report.requestedCoveragePassed, Is.True,
                string.Join("; ", report.limitations));
        }

        private static IEnumerator RunProductionFps(int fps, ContactMatrixReport report)
        {
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            if (existing.IsValid() && existing.isLoaded)
                yield return SceneManager.UnloadSceneAsync(existing);

            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

            EarthMvpDuelController duel = FindInScene<EarthMvpDuelController>(scene);
            if (duel != null) duel.enabled = false;
            EarthMvpBotController botController = FindInScene<EarthMvpBotController>(scene);
            if (botController != null) botController.enabled = false;

            ActorHarness player = ActorHarness.Create(scene, "player", "Planet Character");
            ActorHarness bot = ActorHarness.Create(
                scene,
                "Rumble Linebreaker Bot",
                "Rumble Linebreaker Bot");
            IgnoreActorCollision(player.Root, bot.Root);
            CreateFlatSupportFixture(scene, bot);

            int warmupFrames = Mathf.CeilToInt(0.75f * fps);
            var endOfFrame = new WaitForEndOfFrame();
            for (int frame = 0; frame < warmupFrames; frame++) yield return endOfFrame;
            Assert.That(player.Motor.HasStableSupport, Is.True,
                "Player was not on production support after the deterministic warmup.");
            Assert.That(bot.Motor.HasStableSupport, Is.True,
                "Rumble Linebreaker Bot was not on production support after the deterministic warmup.");

            var playerMetrics = new ActorFpsMetrics(
                "player",
                fps,
                "production-rig-authored-scene-support");
            var botMetrics = new ActorFpsMetrics(
                "Rumble Linebreaker Bot",
                fps,
                "production-rig+synthetic-support-fixture");
            int measuredFrames = Mathf.CeilToInt(ScenarioDurationSeconds * fps);
            double observedDeltaSum = 0d;
            string previousScenario = string.Empty;

            for (int frame = 0; frame < measuredFrames; frame++)
            {
                float elapsed = frame / (float)fps;
                string scenario = ResolveScenario(elapsed);
                bool scenarioEntered = !string.Equals(
                    scenario,
                    previousScenario,
                    StringComparison.Ordinal);
                player.Drive(scenario, scenarioEntered);
                bot.Drive(scenario, scenarioEntered);
                previousScenario = scenario;

                // Read the final rendered pose after Animator IK and every
                // one-writer LateUpdate clamp. Sampling on the next Update mixed
                // the previous foot diagnostic with the current root transform,
                // which manufactured frame-rate-dependent joint jumps.
                yield return endOfFrame;

                float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
                observedDeltaSum += deltaTime;
                SampleActor(report, player, playerMetrics, scenario, fps, frame, elapsed, deltaTime);
                SampleActor(report, bot, botMetrics, scenario, fps, frame, elapsed, deltaTime);
            }

            player.Stop();
            bot.Stop();
            playerMetrics.FinalizeGates();
            botMetrics.FinalizeGates();
            report.fpsRuns.Add(new FpsRunReport
            {
                targetFrameRate = fps,
                requestedDeltaTime = 1f / fps,
                observedMeanDeltaTime = (float)(observedDeltaSum / measuredFrames),
                frameCount = measuredFrames,
                actors = new List<ActorFpsMetrics> { playerMetrics, botMetrics }
            });

            AddActorCoverage(report.coverage, playerMetrics);
            AddActorCoverage(report.coverage, botMetrics);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static void SampleActor(
            ContactMatrixReport report,
            ActorHarness actor,
            ActorFpsMetrics metrics,
            string scenario,
            int fps,
            int frame,
            float elapsed,
            float deltaTime)
        {
            EarthFootContactController foot = actor.Foot;
            AnimatorStateInfo state = actor.Animator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = actor.Animator.GetCurrentAnimatorClipInfo(0);
            string clip = clips.Length > 0 && clips[0].clip != null
                ? clips[0].clip.name
                : "none";
            bool allowedTransition = IsAllowedTransition(scenario, actor, elapsed);

            ActorFrameEvaluation evaluation = metrics.Step(
                actor,
                scenario,
                elapsed,
                deltaTime,
                allowedTransition);
            report.frames.Add(new ContactFrameRecord
            {
                actor = actor.ActorId,
                scenario = scenario,
                targetFps = fps,
                frame = frame,
                elapsedSeconds = elapsed,
                deltaTime = deltaTime,
                animatorStateHash = state.fullPathHash,
                animatorNormalizedTime = state.normalizedTime,
                animatorInTransition = actor.Animator.IsInTransition(0),
                animatorPrimaryClip = clip,
                rootPosition = actor.Root.transform.position,
                rootUp = actor.Motor.LocalUp,
                rootVelocity = actor.Body != null ? actor.Body.linearVelocity : Vector3.zero,
                moveIntent = new Vector2(actor.Motor.LastCommand.Move.x, actor.Motor.LastCommand.Move.y),
                stableSupport = actor.Motor.HasStableSupport,
                leftGaitPhase01 = foot.LeftGaitPhase01,
                rightGaitPhase01 = foot.RightGaitPhase01,
                leftRawContactPoint = foot.LeftRawContactPointWorld,
                rightRawContactPoint = foot.RightRawContactPointWorld,
                leftRawContactNormal = foot.LeftRawContactNormalWorld,
                rightRawContactNormal = foot.RightRawContactNormalWorld,
                leftFilteredContactPoint = foot.LeftFilteredContactPointWorld,
                rightFilteredContactPoint = foot.RightFilteredContactPointWorld,
                leftFilteredContactNormal = foot.LeftFilteredContactNormalWorld,
                rightFilteredContactNormal = foot.RightFilteredContactNormalWorld,
                leftSupportId = foot.LeftSupportId,
                rightSupportId = foot.RightSupportId,
                leftSupportGeneration = foot.LeftSupportGeneration,
                rightSupportGeneration = foot.RightSupportGeneration,
                leftSupportKind = foot.LeftSupportKind.ToString(),
                rightSupportKind = foot.RightSupportKind.ToString(),
                leftSupportLocalAnchor = foot.LeftSupportLocalAnchor,
                rightSupportLocalAnchor = foot.RightSupportLocalAnchor,
                leftActualSupportLocal = foot.LeftActualSupportLocal,
                rightActualSupportLocal = foot.RightActualSupportLocal,
                leftTargetPosition = foot.LeftFilteredContactPointWorld,
                rightTargetPosition = foot.RightFilteredContactPointWorld,
                leftActualBonePosition = foot.LeftActualFootWorld,
                rightActualBonePosition = foot.RightActualFootWorld,
                leftActualBoneRotation = foot.LeftActualFootRotation,
                rightActualBoneRotation = foot.RightActualFootRotation,
                leftIkWeight = foot.LeftFootIkWeight,
                rightIkWeight = foot.RightFootIkWeight,
                leftLocked = foot.LeftFootLocked,
                rightLocked = foot.RightFootLocked,
                leftReason = foot.LeftReason.ToString(),
                rightReason = foot.RightReason.ToString(),
                leftPlantState = foot.LeftPlantState.ToString(),
                rightPlantState = foot.RightPlantState.ToString(),
                pelvisOffsetMeters = foot.PelvisCorrectionMeters,
                leftKneeAngleDegrees = foot.LeftKneeAngleDegrees,
                rightKneeAngleDegrees = foot.RightKneeAngleDegrees,
                leftKneeHintDirection = foot.LeftKneeHintDirectionWorld,
                rightKneeHintDirection = foot.RightKneeHintDirectionWorld,
                leftAnkleAngleDegrees = foot.LeftAnkleAngleDegrees,
                rightAnkleAngleDegrees = foot.RightAnkleAngleDegrees,
                leftPlantedGapMeters = evaluation.leftGap,
                rightPlantedGapMeters = evaluation.rightGap,
                leftFootStepMeters60Hz = evaluation.leftFootStep,
                rightFootStepMeters60Hz = evaluation.rightFootStep,
                leftKneeStepDegrees60Hz = evaluation.leftKneeStep,
                rightKneeStepDegrees60Hz = evaluation.rightKneeStep,
                leftAnkleStepDegrees60Hz = evaluation.leftAnkleStep,
                rightAnkleStepDegrees60Hz = evaluation.rightAnkleStep,
                pelvisStepMeters60Hz = evaluation.pelvisStep,
                discontinuity = evaluation.discontinuity,
                allowedTransition = allowedTransition,
                allowedTransitionState = allowedTransition ? scenario : "none",
                locomoting = foot.IsLocomoting,
                pivoting = foot.IsPivotingInPlace,
                surfing = foot.IsSurfing,
                footPolicy = foot.CurrentFootPolicy.ToString()
            });
        }

        private static string ResolveScenario(float elapsed)
        {
            if (elapsed < 0.50f) return "start-idle-flat";
            if (elapsed < 1.50f) return "walk-run-flat";
            if (elapsed < 2.00f) return "stop-flat";
            if (elapsed < 2.80f) return "sharp-pivot-flat";
            if (elapsed < 4.60f) return "jump-land-flat";
            if (elapsed < 5.30f) return "brace-cast-flat";
            return "surf-interrupt-flat";
        }

        private static bool IsAllowedTransition(
            string scenario,
            ActorHarness actor,
            float elapsed)
        {
            if (scenario == "jump-land-flat") return true;
            if (scenario == "surf-interrupt-flat" && actor.Surf != null && actor.Surf.IsActive)
                return true;
            return false;
        }

        private static void AddActorCoverage(
            List<CoverageRecord> coverage,
            ActorFpsMetrics metrics)
        {
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "start-stop", metrics.productionPipelineMode,
                metrics.startObserved && metrics.stopObserved));
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "walk-run", metrics.productionPipelineMode, metrics.walkRunObserved));
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "sharp-pivot", metrics.productionPipelineMode, metrics.pivotObserved));
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "jump-land", metrics.productionPipelineMode,
                metrics.jumpObserved && metrics.landObserved));
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "brace-cast", metrics.productionPipelineMode, metrics.braceObserved));
            if (metrics.hasProductionSurf)
            {
                coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                    "surf-interrupt", metrics.productionPipelineMode, metrics.surfObserved));
            }
            else
            {
                coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                    "surf-interrupt-support-ownership",
                    "synthetic-support-fixture",
                    RunSolverBoundaryScenario("moving-rotating-support", metrics.targetFps)));
            }
            coverage.Add(new CoverageRecord(metrics.actor, metrics.targetFps,
                "flat", metrics.productionPipelineMode, metrics.flatSupportObserved));
        }

        private static void AddDeterministicEnvironmentBoundaryCoverage(
            int fps,
            List<CoverageRecord> coverage)
        {
            string[] scenarios =
            {
                "slope-15", "slope-30", "step-up-down", "convex-ridge-seam",
                "moving-rotating-support", "support-generation-swap"
            };
            for (int index = 0; index < scenarios.Length; index++)
            {
                string scenario = scenarios[index];
                bool passed = RunSolverBoundaryScenario(scenario, fps);
                coverage.Add(new CoverageRecord(
                    "pair-solver",
                    fps,
                    scenario,
                    "synthetic-support-fixture",
                    passed));
            }
        }

        private static bool RunSolverBoundaryScenario(string scenario, int fps)
        {
            float deltaTime = 1f / fps;
            EarthFootContactState left = default;
            EarthFootContactState right = default;
            bool sawSupportSwap = false;
            bool finite = true;
            float3 normal = scenario == "slope-15"
                ? math.normalize(new float3(0f, math.cos(math.radians(15f)), math.sin(math.radians(15f))))
                : scenario == "slope-30"
                    ? math.normalize(new float3(0f, math.cos(math.radians(30f)), math.sin(math.radians(30f))))
                    : new float3(0f, 1f, 0f);
            int frames = Mathf.CeilToInt(0.75f * fps);
            for (int frame = 0; frame < frames; frame++)
            {
                float phase = frame / (float)frames;
                uint supportId = 91u;
                uint generation = 1u;
                float leftHeight = 0.02f;
                float rightHeight = 0.02f;
                if (scenario == "step-up-down")
                    rightHeight += frame < frames / 2 ? 0.12f : -0.08f;
                if (scenario == "convex-ridge-seam")
                    supportId = frame < frames / 2 ? 91u : 92u;
                if (scenario == "support-generation-swap")
                    generation = frame < frames / 2 ? 1u : 2u;
                float movingOffset = scenario == "moving-rotating-support"
                    ? math.sin(phase * math.PI * 2f) * 0.08f
                    : 0f;
                EarthFootContactInput leftInput = CreateSolverInput(
                    true, phase, new float3(-0.12f, leftHeight, movingOffset), normal,
                    supportId, generation, deltaTime);
                EarthFootContactInput rightInput = CreateSolverInput(
                    false, phase, new float3(0.12f, rightHeight, movingOffset), normal,
                    supportId, generation, deltaTime);
                EarthFootContactPairDecision pair = EarthFootContactSolver.ResolvePair(
                    ref left, ref right, in leftInput, in rightInput);
                sawSupportSwap |= pair.Left.Reason == EarthFootContactReason.SupportSwap ||
                                  pair.Right.Reason == EarthFootContactReason.SupportSwap;
                finite &= math.all(math.isfinite(pair.Left.TargetLocal)) &&
                          math.all(math.isfinite(pair.Right.TargetLocal)) &&
                          !pair.BothLocked;
            }
            if (scenario == "convex-ridge-seam" || scenario == "support-generation-swap")
                return finite && sawSupportSwap;
            return finite;
        }

        private static EarthFootContactInput CreateSolverInput(
            bool left,
            float phase,
            float3 target,
            float3 normal,
            uint supportId,
            uint generation,
            float deltaTime) =>
            new EarthFootContactInput(
                left,
                true,
                true,
                false,
                false,
                true,
                0.02f,
                -0.02f,
                left ? 0.1f : 0f,
                phase,
                target,
                normal,
                target,
                new float3(0f, 1f, 0f),
                supportId,
                generation,
                deltaTime);

        private static void EvaluateCoverage(ContactMatrixReport report)
        {
            bool allRecordedCoveragePassed = true;
            for (int index = 0; index < report.coverage.Count; index++)
                allRecordedCoveragePassed &= report.coverage[index].passed;

            report.fullPipelineEnvironmentCoverage = allRecordedCoveragePassed;
            report.limitations.Add(
                "Slopes 15/30, step up/down, convex seam, moving/rotating support and " +
                "generation swap use deterministic PlayMode synthetic-support fixtures; " +
                "they are not mislabeled as traversal of authored Broken Crown geometry.");
            report.limitations.Add(
                "Rumble Linebreaker Bot has no production EarthSurfController. Its surf-interrupt " +
                "support ownership is covered by the same pair solver on a moving synthetic fixture; " +
                "only the player has authored production surf presentation coverage.");
            report.requestedCoveragePassed = allRecordedCoveragePassed;
        }

        private static bool EveryActorPassesMetricGates(ContactMatrixReport report)
        {
            for (int runIndex = 0; runIndex < report.fpsRuns.Count; runIndex++)
            {
                List<ActorFpsMetrics> actors = report.fpsRuns[runIndex].actors;
                for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
                    if (!actors[actorIndex].hardGatesPassed) return false;
            }
            return true;
        }

        private static void EvaluateCrossFps(ContactMatrixReport report)
        {
            report.crossFpsPassed = true;
            string[] actors = { "player", "Rumble Linebreaker Bot" };
            for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
            {
                ActorFpsMetrics baseline = FindMetrics(report, actors[actorIndex], 60);
                for (int runIndex = 0; runIndex < report.fpsRuns.Count; runIndex++)
                {
                    ActorFpsMetrics candidate = FindMetrics(
                        report,
                        actors[actorIndex],
                        report.fpsRuns[runIndex].targetFrameRate);
                    if (baseline == null || candidate == null) continue;
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "hardGatePass01", baseline.hardGatePass01,
                        candidate.hardGatePass01, 0.01f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "plantedContactEvidenceSeconds", baseline.plantedContactEvidenceSeconds,
                        candidate.plantedContactEvidenceSeconds, 0.1f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "bothLockedLocomotionViolationRate",
                        baseline.bothLockedLocomotionViolationRate,
                        candidate.bothLockedLocomotionViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "prematureRecaptureViolationRate",
                        baseline.prematureRecaptureViolationRate,
                        candidate.prematureRecaptureViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "swingIkViolationRate", baseline.swingIkViolationRate,
                        candidate.swingIkViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "plantedContactViolationRate", baseline.plantedContactViolationRate,
                        candidate.plantedContactViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "supportLocalTargetViolationRate",
                        baseline.supportLocalTargetViolationRate,
                        candidate.supportLocalTargetViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "jointStepViolationRate", baseline.jointStepViolationRate,
                        candidate.jointStepViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "pelvisStepViolationRate", baseline.pelvisStepViolationRate,
                        candidate.pelvisStepViolationRate, 0.001f);
                    AddCrossFps(report, actors[actorIndex], candidate.targetFps,
                        "discontinuityViolationRate", baseline.discontinuityViolationRate,
                        candidate.discontinuityViolationRate, 0.001f);
                }
            }
        }

        private static ActorFpsMetrics FindMetrics(ContactMatrixReport report, string actor, int fps)
        {
            for (int runIndex = 0; runIndex < report.fpsRuns.Count; runIndex++)
            {
                FpsRunReport run = report.fpsRuns[runIndex];
                if (run.targetFrameRate != fps) continue;
                for (int actorIndex = 0; actorIndex < run.actors.Count; actorIndex++)
                    if (run.actors[actorIndex].actor == actor) return run.actors[actorIndex];
            }
            return null;
        }

        private static void AddCrossFps(
            ContactMatrixReport report,
            string actor,
            int fps,
            string metric,
            float baseline,
            float candidate,
            float epsilon)
        {
            float delta = EarthAnimationContactAcceptance.RelativeDelta(
                baseline,
                candidate,
                epsilon);
            bool passed = delta <= EarthAnimationContactAcceptance.MaximumCrossFpsDelta01 + 0.00001f;
            report.crossFps.Add(new CrossFpsRecord
            {
                actor = actor,
                baselineFps = 60,
                candidateFps = fps,
                metric = metric,
                baselineValue = baseline,
                candidateValue = candidate,
                relativeDelta01 = delta,
                comparisonMode = "normalized-acceptance-outcome-rate",
                passed = passed
            });
            report.crossFpsPassed &= passed;
        }

        private static void WriteReport(ContactMatrixReport report)
        {
            string directory = Path.GetFullPath("BuildReports");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string csv = Path.Combine(directory, "AnimationArenaTelemetryLatest.csv");
            string json = Path.Combine(directory, "AnimationArenaTelemetryLatest.json");
            string historicalCsv = Path.Combine(directory, $"AnimationArenaTelemetry-{stamp}.csv");
            string historicalJson = Path.Combine(directory, $"AnimationArenaTelemetry-{stamp}.json");
            string csvText = BuildCsv(report.frames);
            string jsonText = JsonUtility.ToJson(report, true) + Environment.NewLine;
            File.WriteAllText(csv, csvText);
            File.WriteAllText(json, jsonText);
            File.WriteAllText(historicalCsv, csvText);
            File.WriteAllText(historicalJson, jsonText);
            Debug.Log(
                $"[Animation Contact Matrix] rows={report.frames.Count}, " +
                $"metricGates={report.metricHardGatesPassed}, crossFps={report.crossFpsPassed}, " +
                $"coverage={report.requestedCoveragePassed}. JSON: {json}");
        }

        private static string BuildCsv(List<ContactFrameRecord> frames)
        {
            var csv = new StringBuilder(frames.Count * 720);
            csv.AppendLine(
                "schema,actor,scenario,targetFps,frame,elapsedSeconds,deltaTime," +
                "animatorStateHash,animatorNormalizedTime,animatorInTransition,animatorPrimaryClip," +
                "rootX,rootY,rootZ,upX,upY,upZ,velocityX,velocityY,velocityZ," +
                "moveIntentX,moveIntentY,stableSupport," +
                "leftGaitPhase01,rightGaitPhase01," +
                "leftRawX,leftRawY,leftRawZ,rightRawX,rightRawY,rightRawZ," +
                "leftRawNormalX,leftRawNormalY,leftRawNormalZ,rightRawNormalX,rightRawNormalY,rightRawNormalZ," +
                "leftFilteredX,leftFilteredY,leftFilteredZ,rightFilteredX,rightFilteredY,rightFilteredZ," +
                "leftSupportId,rightSupportId,leftSupportGeneration,rightSupportGeneration," +
                "leftSupportKind,rightSupportKind," +
                "leftAnchorX,leftAnchorY,leftAnchorZ,rightAnchorX,rightAnchorY,rightAnchorZ," +
                "leftActualLocalX,leftActualLocalY,leftActualLocalZ," +
                "rightActualLocalX,rightActualLocalY,rightActualLocalZ," +
                "leftActualX,leftActualY,leftActualZ,rightActualX,rightActualY,rightActualZ," +
                "leftIkWeight,rightIkWeight,leftLocked,rightLocked,leftReason,rightReason," +
                "leftPlantState,rightPlantState," +
                "pelvisOffset,leftKneeAngle,rightKneeAngle,leftAnkleAngle,rightAnkleAngle," +
                "leftGap,rightGap,leftFootStep60Hz,rightFootStep60Hz," +
                "leftKneeStep60Hz,rightKneeStep60Hz,leftAnkleStep60Hz,rightAnkleStep60Hz," +
                "pelvisStep60Hz,discontinuity,allowedTransition,allowedTransitionState," +
                "locomoting,pivoting,surfing,footPolicy");
            for (int index = 0; index < frames.Count; index++)
            {
                ContactFrameRecord row = frames[index];
                AppendCsv(csv, row);
            }
            return csv.ToString();
        }

        private static void AppendCsv(StringBuilder csv, ContactFrameRecord row)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            csv.Append("animation-contact-v1,").Append(row.actor).Append(',')
                .Append(row.scenario).Append(',').Append(row.targetFps).Append(',')
                .Append(row.frame).Append(',').Append(row.elapsedSeconds.ToString("F6", c)).Append(',')
                .Append(row.deltaTime.ToString("F6", c)).Append(',')
                .Append(row.animatorStateHash).Append(',')
                .Append(row.animatorNormalizedTime.ToString("F6", c)).Append(',')
                .Append(row.animatorInTransition ? 1 : 0).Append(',')
                .Append(row.animatorPrimaryClip).Append(',');
            AppendVector(csv, row.rootPosition, c);
            AppendVector(csv, row.rootUp, c);
            AppendVector(csv, row.rootVelocity, c);
            csv.Append(row.moveIntent.x.ToString("F6", c)).Append(',')
                .Append(row.moveIntent.y.ToString("F6", c)).Append(',')
                .Append(row.stableSupport ? 1 : 0).Append(',')
                .Append(row.leftGaitPhase01.ToString("F6", c)).Append(',')
                .Append(row.rightGaitPhase01.ToString("F6", c)).Append(',');
            AppendVector(csv, row.leftRawContactPoint, c); AppendVector(csv, row.rightRawContactPoint, c);
            AppendVector(csv, row.leftRawContactNormal, c); AppendVector(csv, row.rightRawContactNormal, c);
            AppendVector(csv, row.leftFilteredContactPoint, c); AppendVector(csv, row.rightFilteredContactPoint, c);
            csv.Append(row.leftSupportId).Append(',').Append(row.rightSupportId).Append(',')
                .Append(row.leftSupportGeneration).Append(',').Append(row.rightSupportGeneration).Append(',')
                .Append(row.leftSupportKind).Append(',').Append(row.rightSupportKind).Append(',');
            AppendVector(csv, row.leftSupportLocalAnchor, c); AppendVector(csv, row.rightSupportLocalAnchor, c);
            AppendVector(csv, row.leftActualSupportLocal, c); AppendVector(csv, row.rightActualSupportLocal, c);
            AppendVector(csv, row.leftActualBonePosition, c); AppendVector(csv, row.rightActualBonePosition, c);
            csv.Append(row.leftIkWeight.ToString("F6", c)).Append(',')
                .Append(row.rightIkWeight.ToString("F6", c)).Append(',')
                .Append(row.leftLocked ? 1 : 0).Append(',').Append(row.rightLocked ? 1 : 0).Append(',')
                .Append(row.leftReason).Append(',').Append(row.rightReason).Append(',')
                .Append(row.leftPlantState).Append(',').Append(row.rightPlantState).Append(',')
                .Append(row.pelvisOffsetMeters.ToString("F6", c)).Append(',')
                .Append(row.leftKneeAngleDegrees.ToString("F6", c)).Append(',')
                .Append(row.rightKneeAngleDegrees.ToString("F6", c)).Append(',')
                .Append(row.leftAnkleAngleDegrees.ToString("F6", c)).Append(',')
                .Append(row.rightAnkleAngleDegrees.ToString("F6", c)).Append(',')
                .Append(row.leftPlantedGapMeters.ToString("F6", c)).Append(',')
                .Append(row.rightPlantedGapMeters.ToString("F6", c)).Append(',')
                .Append(row.leftFootStepMeters60Hz.ToString("F6", c)).Append(',')
                .Append(row.rightFootStepMeters60Hz.ToString("F6", c)).Append(',')
                .Append(row.leftKneeStepDegrees60Hz.ToString("F6", c)).Append(',')
                .Append(row.rightKneeStepDegrees60Hz.ToString("F6", c)).Append(',')
                .Append(row.leftAnkleStepDegrees60Hz.ToString("F6", c)).Append(',')
                .Append(row.rightAnkleStepDegrees60Hz.ToString("F6", c)).Append(',')
                .Append(row.pelvisStepMeters60Hz.ToString("F6", c)).Append(',')
                .Append(row.discontinuity ? 1 : 0).Append(',')
                .Append(row.allowedTransition ? 1 : 0).Append(',')
                .Append(row.allowedTransitionState).Append(',')
                .Append(row.locomoting ? 1 : 0).Append(',')
                .Append(row.pivoting ? 1 : 0).Append(',')
                .Append(row.surfing ? 1 : 0).Append(',')
                .Append(row.footPolicy).AppendLine();
        }

        private static void AppendVector(StringBuilder csv, Vector3 value, CultureInfo c)
        {
            csv.Append(value.x.ToString("F6", c)).Append(',')
                .Append(value.y.ToString("F6", c)).Append(',')
                .Append(value.z.ToString("F6", c)).Append(',');
        }

        private static void IgnoreActorCollision(GameObject a, GameObject b)
        {
            Collider[] left = a.GetComponentsInChildren<Collider>(true);
            Collider[] right = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < left.Length; i++)
                for (int j = 0; j < right.Length; j++)
                    if (left[i] != null && right[j] != null)
                        UnityEngine.Physics.IgnoreCollision(left[i], right[j], true);
        }

        private static void CreateFlatSupportFixture(Scene scene, ActorHarness actor)
        {
            Vector3 up = actor.Root.transform.up.sqrMagnitude > 0.5f
                ? actor.Root.transform.up.normalized
                : Vector3.up;
            Vector3 feet = actor.Motor.SupportFeetPoint(up);
            var support = new GameObject($"Telemetry Flat Support - {actor.ActorId}");
            SceneManager.MoveGameObjectToScene(support, scene);
            support.transform.SetPositionAndRotation(
                feet - up * 0.27f,
                Quaternion.FromToRotation(Vector3.up, up));
            var collider = support.AddComponent<BoxCollider>();
            collider.size = new Vector3(6f, 0.5f, 6f);
            if (actor.Body != null)
            {
                // This fixture evaluates the production Humanoid/IK pipeline in
                // place. The Test Runner does not guarantee the same number of
                // automatic physics ticks per rendered frame, so allowing the
                // body to traverse the curved spawn made 30/60/120 sample three
                // different world locations. Input, gait, jump/support state and
                // the visible rig still run; only fixture translation is pinned.
                actor.Body.linearVelocity = Vector3.zero;
                actor.Body.constraints |= RigidbodyConstraints.FreezePosition;
            }
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                    if (transforms[index].name == objectName) return transforms[index].gameObject;
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

        private sealed class ActorHarness
        {
            public string ActorId;
            public GameObject Root;
            public PlanetMotor Motor;
            public Rigidbody Body;
            public HumanoidCharacterPresentation Presentation;
            public EarthFootContactController Foot;
            public Animator Animator;
            public EarthSurfController Surf;
            public TelemetryMotorInput Input;
            public TelemetryFootPolicyDriver Policy;
            public float2 LocomotionMove;
            private bool _surfAttempted;

            public static ActorHarness Create(Scene scene, string actorId, string objectName)
            {
                GameObject root = FindByName(scene, objectName);
                Assert.That(root, Is.Not.Null, $"Missing production actor '{objectName}'.");
                PlanetMotor motor = root.GetComponent<PlanetMotor>();
                HumanoidCharacterPresentation presentation =
                    root.GetComponentInChildren<HumanoidCharacterPresentation>(true);
                Assert.That(motor, Is.Not.Null, $"{objectName} has no PlanetMotor.");
                Assert.That(presentation, Is.Not.Null,
                    $"{objectName} has no HumanoidCharacterPresentation.");
                Assert.That(presentation.FootContactController, Is.Not.Null,
                    $"{objectName} has no independent EarthFootContactController.");
                var input = root.AddComponent<TelemetryMotorInput>();
                motor.ConfigureInputSource(input);
                var policy = presentation.gameObject.AddComponent<TelemetryFootPolicyDriver>();
                policy.Controller = presentation.FootContactController;
                return new ActorHarness
                {
                    ActorId = actorId,
                    Root = root,
                    Motor = motor,
                    Body = motor.GetComponent<Rigidbody>(),
                    Presentation = presentation,
                    Foot = presentation.FootContactController,
                    Animator = presentation.Animator,
                    Surf = root.GetComponent<EarthSurfController>(),
                    Input = input,
                    Policy = policy,
                    // The authored bot spawn sits near the positive camera-forward
                    // rim. Driving the same +Z command as the player makes the
                    // 30-Hz run leave the walkable arena before contact can be
                    // evaluated. Drive inward through the same production motor.
                    LocomotionMove = actorId == "Rumble Linebreaker Bot"
                        ? new float2(-0.32f, -1f)
                        : new float2(0.32f, 1f)
                };
            }

            public void Drive(string scenario, bool entered)
            {
                Input.Move = float2.zero;
                Policy.DesiredPolicy = EarthAuthoredFootPolicy.DefaultContact;
                Motor.SetCastStance(0f);
                if (scenario == "walk-run-flat") Input.Move = LocomotionMove;
                else if (scenario == "sharp-pivot-flat") Input.Move = new float2(1f, 0f);
                else if (scenario == "jump-land-flat" && entered) Input.RequestJump();
                else if (scenario == "brace-cast-flat")
                {
                    Policy.DesiredPolicy = EarthAuthoredFootPolicy.BraceBoth;
                    Motor.SetCastStance(1f);
                }
                else if (scenario == "surf-interrupt-flat")
                {
                    if (!_surfAttempted)
                    {
                        _surfAttempted = true;
                        Surf?.Begin(Time.time, Motor.FacingForward);
                    }
                    if (Surf != null && Surf.IsActive)
                        Surf.Continue(new Vector2(0f, 0.65f), Motor.FacingForward);
                }
                if (entered && scenario == "stop-flat") Motor.SettleTangentialMotion();
            }

            public void Stop()
            {
                Input.Move = float2.zero;
                Policy.DesiredPolicy = EarthAuthoredFootPolicy.DefaultContact;
                Motor.SetCastStance(0f);
                Surf?.Cancel();
            }
        }

        [DefaultExecutionOrder(900)]
        private sealed class TelemetryFootPolicyDriver : MonoBehaviour
        {
            public EarthFootContactController Controller;
            public EarthAuthoredFootPolicy DesiredPolicy;

            private void Update() => Controller?.SetAuthoredFootPolicy(DesiredPolicy);
        }

        private sealed class TelemetryMotorInput : MonoBehaviour, IPlanetMotorInputSource
        {
            public float2 Move;
            private int _jumpSamples;

            public void RequestJump() => _jumpSamples = 1;

            public PlanetMotorCommand SampleCommand(uint tick)
            {
                bool jump = _jumpSamples > 0;
                if (_jumpSamples > 0) _jumpSamples--;
                return new PlanetMotorCommand(tick, Move, jump);
            }
        }

        [Serializable]
        private sealed class ContactMatrixReport
        {
            public string schema;
            public string utc;
            public string scene;
            public string captureControls;
            public string crossFpsComparison;
            public List<FpsRunReport> fpsRuns;
            public List<ContactFrameRecord> frames;
            public List<CoverageRecord> coverage;
            public List<CrossFpsRecord> crossFps;
            public List<string> limitations;
            public bool metricHardGatesPassed;
            public bool crossFpsPassed;
            public bool fullPipelineEnvironmentCoverage;
            public bool requestedCoveragePassed;
            public bool passed;
        }

        [Serializable]
        private sealed class FpsRunReport
        {
            public int targetFrameRate;
            public float requestedDeltaTime;
            public float observedMeanDeltaTime;
            public int frameCount;
            public List<ActorFpsMetrics> actors;
        }

        [Serializable]
        private sealed class ActorFpsMetrics
        {
            public string actor;
            public int targetFps;
            public string productionPipelineMode;
            public int sampledFrames;
            public int bothLockedLocomotionFrames;
            public int prematureRecaptures;
            public float minimumReleaseRecaptureSeconds = float.PositiveInfinity;
            public float maximumSwingIkWeight;
            public float maximumPlantedDriftMeters;
            public float minimumPlantedGapMeters = float.PositiveInfinity;
            public float maximumPlantedGapMeters = float.NegativeInfinity;
            public float maximumSupportLocalTargetStepMeters;
            public float maximumKneeStepDegrees;
            public float maximumAnkleStepDegrees;
            public float maximumPelvisStepMeters;
            public float meanPlantedDriftMeters;
            public float meanSupportLocalTargetStepMeters;
            public float meanKneeStepDegrees;
            public float meanAnkleStepDegrees;
            public float meanPelvisStepMeters;
            public int plantedSampleCount;
            public int supportLocalTargetStepSampleCount;
            public int jointStepSampleCount;
            public int pelvisStepSampleCount;
            public float plantedContactEvidenceSeconds;
            public float hardGatePass01;
            public float bothLockedLocomotionViolationRate;
            public float prematureRecaptureViolationRate;
            public float swingIkViolationRate;
            public float plantedContactViolationRate;
            public float supportLocalTargetViolationRate;
            public float jointStepViolationRate;
            public float pelvisStepViolationRate;
            public float discontinuityViolationRate;
            public int unallowedDiscontinuities;
            public bool startObserved;
            public bool stopObserved;
            public bool walkRunObserved;
            public bool pivotObserved;
            public bool jumpObserved;
            public bool landObserved;
            public bool braceObserved;
            public bool surfObserved;
            public bool hasProductionSurf;
            public bool flatSupportObserved;
            public bool hardGatesPassed;

            [NonSerialized] private FootHistory _left;
            [NonSerialized] private FootHistory _right;
            [NonSerialized] private bool _hasPreviousPelvis;
            [NonSerialized] private float _previousPelvis;
            [NonSerialized] private bool _wasAirborne;
            [NonSerialized] private float _plantedDriftSum;
            [NonSerialized] private int _plantedDriftCount;
            [NonSerialized] private float _targetStepSum;
            [NonSerialized] private int _targetStepCount;
            [NonSerialized] private float _kneeStepSum;
            [NonSerialized] private float _ankleStepSum;
            [NonSerialized] private int _jointStepCount;
            [NonSerialized] private float _pelvisStepSum;
            [NonSerialized] private int _pelvisStepCount;
            [NonSerialized] private int _locomotionFrameCount;
            [NonSerialized] private int _recaptureEventCount;
            [NonSerialized] private int _swingSampleCount;
            [NonSerialized] private int _swingViolationCount;
            [NonSerialized] private int _plantedViolationCount;
            [NonSerialized] private int _targetStepViolationCount;
            [NonSerialized] private int _jointStepViolationCount;
            [NonSerialized] private int _pelvisStepViolationCount;

            public ActorFpsMetrics(string actorId, int fps, string pipelineMode)
            {
                actor = actorId;
                targetFps = fps;
                productionPipelineMode = pipelineMode;
                _left = new FootHistory();
                _right = new FootHistory();
            }

            public ActorFrameEvaluation Step(
                ActorHarness harness,
                string scenario,
                float elapsed,
                float deltaTime,
                bool allowedTransition)
            {
                sampledFrames++;
                EarthFootContactController foot = harness.Foot;
                startObserved |= scenario == "start-idle-flat";
                stopObserved |= scenario == "stop-flat" &&
                                Vector3.ProjectOnPlane(
                                    harness.Body.linearVelocity,
                                    harness.Motor.LocalUp).magnitude < 0.75f;
                walkRunObserved |= scenario == "walk-run-flat" &&
                                   (foot.IsLocomoting || harness.Animator.GetFloat("Speed") > 0.20f);
                pivotObserved |= scenario == "sharp-pivot-flat" && foot.IsPivotingInPlace;
                bool supported = harness.Motor.HasStableSupport;
                jumpObserved |= scenario == "jump-land-flat" && !supported;
                if (_wasAirborne && supported) landObserved = true;
                _wasAirborne |= !supported;
                braceObserved |= scenario == "brace-cast-flat" &&
                                  foot.CurrentFootPolicy == EarthAuthoredFootPolicy.BraceBoth;
                surfObserved |= scenario == "surf-interrupt-flat" && foot.IsSurfing;
                hasProductionSurf |= harness.Surf != null;
                flatSupportObserved |= Vector3.Dot(
                    foot.LeftRawContactNormalWorld,
                    harness.Motor.LocalUp) > 0.95f ||
                    Vector3.Dot(foot.RightRawContactNormalWorld, harness.Motor.LocalUp) > 0.95f;
                if (foot.IsLocomoting)
                {
                    _locomotionFrameCount++;
                    if (foot.LeftFootLocked && foot.RightFootLocked)
                        bothLockedLocomotionFrames++;
                }

                FootEvaluation left = StepFoot(
                    _left,
                    foot.LeftFootLocked,
                    foot.LeftReason,
                    foot.LeftFootIkWeight,
                    foot.LeftSupportId,
                    foot.LeftSupportGeneration,
                    foot.LeftSupportLocalAnchor,
                    foot.LeftActualSupportLocal,
                    foot.LeftActualFootWorld,
                    foot.LeftFilteredContactPointWorld,
                    foot.LeftFilteredContactNormalWorld,
                    foot.LeftKneeAngleDegrees,
                    foot.LeftAnkleAngleDegrees,
                    foot.LeftKneeHintDirectionWorld,
                    Quaternion.Inverse(harness.Animator.transform.rotation) *
                    foot.LeftActualFootRotation,
                    harness.Animator.transform.InverseTransformPoint(foot.LeftActualFootWorld),
                    scenario,
                    elapsed,
                    deltaTime,
                    allowedTransition);
                FootEvaluation right = StepFoot(
                    _right,
                    foot.RightFootLocked,
                    foot.RightReason,
                    foot.RightFootIkWeight,
                    foot.RightSupportId,
                    foot.RightSupportGeneration,
                    foot.RightSupportLocalAnchor,
                    foot.RightActualSupportLocal,
                    foot.RightActualFootWorld,
                    foot.RightFilteredContactPointWorld,
                    foot.RightFilteredContactNormalWorld,
                    foot.RightKneeAngleDegrees,
                    foot.RightAnkleAngleDegrees,
                    foot.RightKneeHintDirectionWorld,
                    Quaternion.Inverse(harness.Animator.transform.rotation) *
                    foot.RightActualFootRotation,
                    harness.Animator.transform.InverseTransformPoint(foot.RightActualFootWorld),
                    scenario,
                    elapsed,
                    deltaTime,
                    allowedTransition);
                float pelvisStep = 0f;
                if (_hasPreviousPelvis)
                {
                    pelvisStep = EarthAnimationContactAcceptance.NormalizeTo60Hz(
                        Mathf.Abs(foot.PelvisCorrectionMeters - _previousPelvis),
                        deltaTime);
                    if (!allowedTransition)
                    {
                        _pelvisStepSum += pelvisStep;
                        _pelvisStepCount++;
                        if (pelvisStep > EarthAnimationContactAcceptance.MaximumPelvisStepAt60Hz +
                            0.0001f)
                            _pelvisStepViolationCount++;
                    }
                }
                _hasPreviousPelvis = true;
                _previousPelvis = foot.PelvisCorrectionMeters;
                if (!allowedTransition)
                    maximumPelvisStepMeters = Mathf.Max(maximumPelvisStepMeters, pelvisStep);
                bool discontinuity = left.discontinuity || right.discontinuity ||
                                     (!allowedTransition && pelvisStep >
                                      EarthAnimationContactAcceptance.MaximumPelvisStepAt60Hz);
                if (discontinuity) unallowedDiscontinuities++;
                return new ActorFrameEvaluation
                {
                    leftGap = left.gap,
                    rightGap = right.gap,
                    leftFootStep = left.footStep,
                    rightFootStep = right.footStep,
                    leftKneeStep = left.kneeStep,
                    rightKneeStep = right.kneeStep,
                    leftAnkleStep = left.ankleStep,
                    rightAnkleStep = right.ankleStep,
                    pelvisStep = pelvisStep,
                    discontinuity = discontinuity
                };
            }

            private FootEvaluation StepFoot(
                FootHistory history,
                bool locked,
                EarthFootContactReason reason,
                float weight,
                uint supportId,
                uint supportGeneration,
                Vector3 targetLocal,
                Vector3 actualLocal,
                Vector3 actualWorld,
                Vector3 targetWorld,
                Vector3 targetNormal,
                float kneeAngle,
                float ankleAngle,
                Vector3 kneeDirection,
                Quaternion ankleRotation,
                Vector3 rootLocal,
                string scenario,
                float elapsed,
                float deltaTime,
                bool allowedTransition)
            {
                bool contactGateScenario = scenario == "walk-run-flat" ||
                                           scenario == "sharp-pivot-flat";
                if (contactGateScenario && history.initialized && history.locked && !locked)
                    history.lastReleaseSeconds = elapsed;
                if (contactGateScenario && history.initialized && !history.locked && locked &&
                    history.lastReleaseSeconds >= 0f)
                {
                    float interval = elapsed - history.lastReleaseSeconds;
                    minimumReleaseRecaptureSeconds = Mathf.Min(
                        minimumReleaseRecaptureSeconds,
                        interval);
                    if (interval + 0.0001f <
                        EarthAnimationContactAcceptance.MinimumReleaseRecaptureSeconds)
                        prematureRecaptures++;
                    _recaptureEventCount++;
                }
                if (contactGateScenario &&
                    !locked && reason == EarthFootContactReason.Swing)
                {
                    maximumSwingIkWeight = Mathf.Max(maximumSwingIkWeight, weight);
                    _swingSampleCount++;
                    if (weight > EarthAnimationContactAcceptance.MaximumSwingIkWeight + 0.0001f)
                        _swingViolationCount++;
                }
                if (!contactGateScenario) history.lastReleaseSeconds = -1f;

                float gap = float.NaN;
                if (contactGateScenario && locked && weight >= 0.90f)
                {
                    float drift = Vector3.Distance(actualLocal, targetLocal);
                    maximumPlantedDriftMeters = Mathf.Max(maximumPlantedDriftMeters, drift);
                    _plantedDriftSum += drift;
                    _plantedDriftCount++;
                    gap = Vector3.Dot(actualWorld - targetWorld, targetNormal.normalized);
                    minimumPlantedGapMeters = Mathf.Min(minimumPlantedGapMeters, gap);
                    maximumPlantedGapMeters = Mathf.Max(maximumPlantedGapMeters, gap);
                    if (drift > EarthAnimationContactAcceptance.MaximumPlantedDriftMeters + 0.0001f ||
                        !EarthAnimationContactAcceptance.IsPlantedGapAccepted(gap))
                        _plantedViolationCount++;
                    if (history.initialized && history.locked &&
                        history.supportId == supportId && history.supportGeneration == supportGeneration)
                    {
                        float step = EarthAnimationContactAcceptance.NormalizeTo60Hz(
                            Vector3.Distance(targetLocal, history.targetLocal),
                            deltaTime);
                        maximumSupportLocalTargetStepMeters = Mathf.Max(
                            maximumSupportLocalTargetStepMeters,
                            step);
                        _targetStepSum += step;
                        _targetStepCount++;
                        if (step > EarthAnimationContactAcceptance.MaximumSupportLocalTargetStepAt60Hz +
                            0.0001f)
                            _targetStepViolationCount++;
                    }
                }

                float footStep = 0f;
                float kneeStep = 0f;
                float ankleStep = 0f;
                if (history.initialized)
                {
                    footStep = EarthAnimationContactAcceptance.NormalizeTo60Hz(
                        Vector3.Distance(rootLocal, history.rootLocal),
                        deltaTime);
                    kneeStep = EarthAnimationContactAcceptance.NormalizeTo60Hz(
                        Vector3.Angle(history.kneeDirection, kneeDirection),
                        deltaTime);
                    ankleStep = EarthAnimationContactAcceptance.NormalizeTo60Hz(
                        Quaternion.Angle(history.ankleRotation, ankleRotation),
                        deltaTime);
                    if (!allowedTransition)
                    {
                        maximumKneeStepDegrees = Mathf.Max(maximumKneeStepDegrees, kneeStep);
                        maximumAnkleStepDegrees = Mathf.Max(maximumAnkleStepDegrees, ankleStep);
                        _kneeStepSum += kneeStep;
                        _ankleStepSum += ankleStep;
                        _jointStepCount++;
                        if (kneeStep > EarthAnimationContactAcceptance.MaximumJointStepDegreesAt60Hz +
                            0.0001f ||
                            ankleStep > EarthAnimationContactAcceptance.MaximumJointStepDegreesAt60Hz +
                            0.0001f)
                            _jointStepViolationCount++;
                    }
                }
                bool discontinuity = EarthAnimationContactAcceptance.IsUnallowedDiscontinuity(
                    footStep,
                    kneeStep,
                    ankleStep,
                    0f,
                    allowedTransition);
                history.initialized = true;
                history.locked = locked;
                history.supportId = supportId;
                history.supportGeneration = supportGeneration;
                history.targetLocal = targetLocal;
                history.rootLocal = rootLocal;
                history.kneeAngle = kneeAngle;
                history.ankleAngle = ankleAngle;
                history.kneeDirection = kneeDirection;
                history.ankleRotation = ankleRotation;
                return new FootEvaluation
                {
                    gap = gap,
                    footStep = footStep,
                    kneeStep = kneeStep,
                    ankleStep = ankleStep,
                    discontinuity = discontinuity
                };
            }

            public void FinalizeGates()
            {
                if (float.IsPositiveInfinity(minimumReleaseRecaptureSeconds))
                    minimumReleaseRecaptureSeconds = -1f;
                if (float.IsPositiveInfinity(minimumPlantedGapMeters))
                    minimumPlantedGapMeters = 0f;
                if (float.IsNegativeInfinity(maximumPlantedGapMeters))
                    maximumPlantedGapMeters = 0f;
                meanPlantedDriftMeters = _plantedDriftCount > 0
                    ? _plantedDriftSum / _plantedDriftCount
                    : 0f;
                meanSupportLocalTargetStepMeters = _targetStepCount > 0
                    ? _targetStepSum / _targetStepCount
                    : 0f;
                meanKneeStepDegrees = _jointStepCount > 0
                    ? _kneeStepSum / _jointStepCount
                    : 0f;
                meanAnkleStepDegrees = _jointStepCount > 0
                    ? _ankleStepSum / _jointStepCount
                    : 0f;
                meanPelvisStepMeters = _pelvisStepCount > 0
                    ? _pelvisStepSum / _pelvisStepCount
                    : 0f;
                plantedSampleCount = _plantedDriftCount;
                supportLocalTargetStepSampleCount = _targetStepCount;
                jointStepSampleCount = _jointStepCount;
                pelvisStepSampleCount = _pelvisStepCount;
                plantedContactEvidenceSeconds = plantedSampleCount / (float)targetFps;
                bool recapturePassed = minimumReleaseRecaptureSeconds < 0f ||
                    minimumReleaseRecaptureSeconds + 0.0001f >=
                    EarthAnimationContactAcceptance.MinimumReleaseRecaptureSeconds;
                hardGatesPassed = bothLockedLocomotionFrames == 0 &&
                    prematureRecaptures == 0 && recapturePassed &&
                    maximumSwingIkWeight <=
                    EarthAnimationContactAcceptance.MaximumSwingIkWeight + 0.0001f &&
                    maximumPlantedDriftMeters <=
                    EarthAnimationContactAcceptance.MaximumPlantedDriftMeters + 0.0001f &&
                    EarthAnimationContactAcceptance.IsPlantedGapAccepted(minimumPlantedGapMeters) &&
                    EarthAnimationContactAcceptance.IsPlantedGapAccepted(maximumPlantedGapMeters) &&
                    maximumSupportLocalTargetStepMeters <=
                    EarthAnimationContactAcceptance.MaximumSupportLocalTargetStepAt60Hz + 0.0001f &&
                    maximumKneeStepDegrees <=
                    EarthAnimationContactAcceptance.MaximumJointStepDegreesAt60Hz + 0.0001f &&
                    maximumAnkleStepDegrees <=
                    EarthAnimationContactAcceptance.MaximumJointStepDegreesAt60Hz + 0.0001f &&
                    maximumPelvisStepMeters <=
                    EarthAnimationContactAcceptance.MaximumPelvisStepAt60Hz + 0.0001f &&
                    unallowedDiscontinuities == 0;
                hardGatePass01 = hardGatesPassed ? 1f : 0f;
                bothLockedLocomotionViolationRate = SafeRate(
                    bothLockedLocomotionFrames,
                    _locomotionFrameCount);
                prematureRecaptureViolationRate = SafeRate(
                    prematureRecaptures,
                    _recaptureEventCount);
                swingIkViolationRate = SafeRate(_swingViolationCount, _swingSampleCount);
                plantedContactViolationRate = SafeRate(
                    _plantedViolationCount,
                    _plantedDriftCount);
                supportLocalTargetViolationRate = SafeRate(
                    _targetStepViolationCount,
                    _targetStepCount);
                jointStepViolationRate = SafeRate(
                    _jointStepViolationCount,
                    _jointStepCount);
                pelvisStepViolationRate = SafeRate(
                    _pelvisStepViolationCount,
                    _pelvisStepCount);
                discontinuityViolationRate = SafeRate(
                    unallowedDiscontinuities,
                    sampledFrames);
            }

            private static float SafeRate(int violations, int samples) =>
                samples > 0 ? violations / (float)samples : 0f;
        }

        private sealed class FootHistory
        {
            public bool initialized;
            public bool locked;
            public uint supportId;
            public uint supportGeneration;
            public float lastReleaseSeconds = -1f;
            public Vector3 targetLocal;
            public Vector3 rootLocal;
            public float kneeAngle;
            public float ankleAngle;
            public Vector3 kneeDirection;
            public Quaternion ankleRotation;
        }

        private struct FootEvaluation
        {
            public float gap;
            public float footStep;
            public float kneeStep;
            public float ankleStep;
            public bool discontinuity;
        }

        private struct ActorFrameEvaluation
        {
            public float leftGap;
            public float rightGap;
            public float leftFootStep;
            public float rightFootStep;
            public float leftKneeStep;
            public float rightKneeStep;
            public float leftAnkleStep;
            public float rightAnkleStep;
            public float pelvisStep;
            public bool discontinuity;
        }

        [Serializable]
        private sealed class ContactFrameRecord
        {
            public string actor;
            public string scenario;
            public int targetFps;
            public int frame;
            public float elapsedSeconds;
            public float deltaTime;
            public int animatorStateHash;
            public float animatorNormalizedTime;
            public bool animatorInTransition;
            public string animatorPrimaryClip;
            public Vector3 rootPosition;
            public Vector3 rootUp;
            public Vector3 rootVelocity;
            public Vector2 moveIntent;
            public bool stableSupport;
            public float leftGaitPhase01;
            public float rightGaitPhase01;
            public Vector3 leftRawContactPoint;
            public Vector3 rightRawContactPoint;
            public Vector3 leftRawContactNormal;
            public Vector3 rightRawContactNormal;
            public Vector3 leftFilteredContactPoint;
            public Vector3 rightFilteredContactPoint;
            public Vector3 leftFilteredContactNormal;
            public Vector3 rightFilteredContactNormal;
            public uint leftSupportId;
            public uint rightSupportId;
            public uint leftSupportGeneration;
            public uint rightSupportGeneration;
            public string leftSupportKind;
            public string rightSupportKind;
            public Vector3 leftSupportLocalAnchor;
            public Vector3 rightSupportLocalAnchor;
            public Vector3 leftActualSupportLocal;
            public Vector3 rightActualSupportLocal;
            public Vector3 leftTargetPosition;
            public Vector3 rightTargetPosition;
            public Vector3 leftActualBonePosition;
            public Vector3 rightActualBonePosition;
            public Quaternion leftActualBoneRotation;
            public Quaternion rightActualBoneRotation;
            public float leftIkWeight;
            public float rightIkWeight;
            public bool leftLocked;
            public bool rightLocked;
            public string leftReason;
            public string rightReason;
            public string leftPlantState;
            public string rightPlantState;
            public float pelvisOffsetMeters;
            public float leftKneeAngleDegrees;
            public float rightKneeAngleDegrees;
            public Vector3 leftKneeHintDirection;
            public Vector3 rightKneeHintDirection;
            public float leftAnkleAngleDegrees;
            public float rightAnkleAngleDegrees;
            public float leftPlantedGapMeters;
            public float rightPlantedGapMeters;
            public float leftFootStepMeters60Hz;
            public float rightFootStepMeters60Hz;
            public float leftKneeStepDegrees60Hz;
            public float rightKneeStepDegrees60Hz;
            public float leftAnkleStepDegrees60Hz;
            public float rightAnkleStepDegrees60Hz;
            public float pelvisStepMeters60Hz;
            public bool discontinuity;
            public bool allowedTransition;
            public string allowedTransitionState;
            public bool locomoting;
            public bool pivoting;
            public bool surfing;
            public string footPolicy;
        }

        [Serializable]
        private sealed class CoverageRecord
        {
            public string actor;
            public int targetFps;
            public string scenario;
            public string evidenceMode;
            public string pipelineMode;
            public bool passed;

            public CoverageRecord(
                string actorId,
                int fps,
                string scenarioId,
                string mode,
                bool accepted)
            {
                actor = actorId;
                targetFps = fps;
                scenario = scenarioId;
                evidenceMode = mode;
                pipelineMode = mode;
                passed = accepted;
            }
        }

        [Serializable]
        private sealed class CrossFpsRecord
        {
            public string actor;
            public int baselineFps;
            public int candidateFps;
            public string metric;
            public float baselineValue;
            public float candidateValue;
            public float relativeDelta01;
            public string comparisonMode;
            public bool passed;
        }
    }
}
