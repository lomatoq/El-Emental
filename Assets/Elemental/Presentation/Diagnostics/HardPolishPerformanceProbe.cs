using System;
using Elemental.Runtime.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Elemental.Input.Actions;
using Elemental.Input.Gestures;
using Elemental.Presentation.Fire;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Diagnostics;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Networking;
using Elemental.Presentation.UI;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Elemental.Presentation.Diagnostics
{
    /// <summary>Explicit standalone QA entry point. Inert unless launched with --hard-polish-perf.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class HardPolishPerformanceProbe : MonoBehaviour
    {
        public const string Scenario = "hard-polish-1080-v1";
        private const int Capacity = 120000, Seed = 73191;
        [Serializable] public sealed class NativeGcFile
        {
            public string path,reason,status;
            public int phase,firstEngineFrame,lastEngineFrame;
            public double started,ended;
            public bool startCalibration,endCalibration;
            public int measuredFrames,goldActiveFrames,goldVisibleFrames,stageMainFrames,stageResultFrames,
                stageStoneFrames,fireActiveFrames,fireDrainingFrames;
            public int goldCueDelta,goldCompletedDelta,fireBeginDelta,sparkCueDelta,audioCueDelta;
            public uint resultGenerationDelta;
            public int peakGoldEffects,peakGoldVisible,peakStageStones,peakFireViews,peakFireDraining;
        }
        [Serializable] public sealed class Metric { public string name, unit; public int samples; public double p50, p95, p99, maximum; }
        [Serializable] public sealed class Phase { public string name; public int frames; public Metric[] metrics; }
        [Serializable] public sealed class Report
        {
            public string scenario, revision, buildGuid, cosmeticMode, cosmeticScope, utc, platform, api, gpu, cpu, quality, unity, renderer, sceneManifest, status;
            public int width, height, frameCap, vSync, seed, transitions, destructionTargets, releasedPieces, botSpells, fireBegins, frameCapacity;
            public float dayTime, renderScale;
            public bool standalone, captureOverflow, scopedAllocationMeasurement;
            public HardPolishAllocationCounters.Sample[] scopedAllocations;
            public string allocationCounterStatus;public long allocationCounterCalibrationBytes,allocationCounterSmallCalibrationBytes;
            public string gcMeaning = "gc frame metric remains whole-frame. Optional scopedAllocations are inclusive synchronous managed bytes on measured main-thread callbacks, non-additive when nested. UNSUPPORTED_RUNTIME_COUNTER and UNEXERCISED are not zero. Counter requires a cold escaping73728-byte positive control before any zero claim. Async/native allocations and uninstrumented owners remain outside this evidence.";
            public bool nativeGcCapture;public NativeGcFile[] nativeGcFiles;
            public Phase[] phases;
        }
        private struct Frame { public int phase; public double time, main, render, gpu, gc, wind, responses; public Vector3 cameraPosition; public Quaternion cameraRotation; public float fieldOfView; }
        private readonly Frame[] frames = new Frame[Capacity];
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private readonly double[] scratch = new double[Capacity];
        private ProfilerRecorder main, render, gc, wind, responses;
        private FrontendFlowController flow; private EarthMvpDuelController duel;
        private FireStreamPresentationBinding fire; private EarthInputAdapter adapter; private MagicInputController magic;
        private EarthMvpBotController bot; private UnityEngine.Camera camera;
        private GoldRespawnPresenter gold; private MatchPresentationStage stage;
        private EarthMagicFeedback feedback; private EarthAudioDirector localAudio; private EarthDuelHud hud;
        private bool disableNewCosmetics, measureAllocations,nativeGc;
        private HardPolishNativeGcCapture nativeCapture;
        private NativeGcFile nativeFile;
        private int nativeFrames;
        private readonly List<NativeGcFile> nativeFiles=new List<NativeGcFile>();
        private readonly bool[] nativeRequests=new bool[15];
        private int nativeGoldCues,nativeGoldCompleted,nativeFireBegins,nativeSparks,nativeAudio;
        private uint nativeResultGeneration;
        private bool nativeReplicaAuthorityOwned,nativePriorAuthority;
        private EarthArenaStructure[] structures; private EarthDestructibleDecorRock[] decor;
        private Report report; private string output; private int phase, count, skipFrames, destructionIndex;
        private bool capture, replay, wasPlayable, destroying, finished;
        private uint sequence; private EarthInputBits previousHeld;
        private int observedBotStrikes,lastBotStrikeCount;private bool observeBotStrikes;
        private ElementId lastRequestedSchool;
        private bool schoolRequested;
        private double combatStart; private ulong lastGpuTimestamp;
        private readonly string[] phases = { "steady", "combat", "heavy", "transitions" };
        public static string Argument(string name, string fallback = null)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartExplicitProbe()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("--hard-polish-perf")) return;
            new GameObject("Explicit hard polish performance QA").AddComponent<HardPolishPerformanceProbe>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SeedExplicitProbe()
        {
            if (!Application.isEditor && Environment.GetCommandLineArgs().Contains("--hard-polish-perf")) UnityEngine.Random.InitState(Seed);
        }
        private T One<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        private IEnumerator Start()
        {
            // Flatten nested iterators here so Unity cannot execute their MoveNext
            // outside this guard. Capacity is allocated once, before measured work.
            var routines = new Stack<IEnumerator>(8);
            routines.Push(Run());
            Exception failure = null;
            try
            {
                while (routines.Count > 0 && !finished)
                {
                    object next = null;
                    bool yielded = false;
                    try
                    {
                        IEnumerator current = routines.Peek();
                        if (!current.MoveNext())
                        {
                            routines.Pop();
                            (current as IDisposable)?.Dispose();
                        }
                        else
                        {
                            next = current.Current;
                            if (next is IEnumerator nested) routines.Push(nested);
                            else yielded = true;
                        }
                    }
                    catch (Exception error) { failure = error; break; }
                    if (yielded) yield return next;
                }
            }
            finally
            {
                // Replica authority and other nested finally owners unwind even if
                // MoveNext/Current/Dispose throws, or Unity stops this coroutine.
                while (routines.Count > 0)
                {
                    try { (routines.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { if (failure == null) failure = error; }
                }
            }
            if (failure != null) Fail(failure.ToString());
        }
        private IEnumerator Run()
        {
            string revision = Argument("--perf-revision");
            if (string.IsNullOrWhiteSpace(revision) || revision.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            { Fail("Provide the verified commit/worktree identity with --perf-revision."); yield break; }
            output = Path.GetFullPath(Argument("--perf-output", "BuildReports/HardPolish/" + revision)); Directory.CreateDirectory(output);
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed); UnityEngine.Random.InitState(Seed);
            yield return null; yield return null;
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Screen.width != 1920 || Screen.height != 1080)
            { Fail("This gate requires rendered standalone 1920x1080, not batch/headless or editor timings."); yield break; }
            if (SceneManager.GetActiveScene().name != "EarthCoreSlice") { Fail("Build the existing EarthCoreSlice as the launch scene."); yield break; }
            flow = One<FrontendFlowController>(); duel = flow.MatchController; fire = One<FireStreamPresentationBinding>();
            var ready = One<EarthSceneReadinessGate>();
            // Serialized production Celestial.TargetCamera and CinematicMenuCamera.outputCamera share fileID 1967943495.
            camera = One<CelestialSystemBehaviour>().TargetCamera;
            magic = duel.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single(); adapter = magic.GetComponent<EarthInputAdapter>();
            gold = duel.GetComponent<GoldRespawnPresenter>(); stage = One<MatchPresentationStage>();
            feedback = One<EarthMagicFeedback>(); localAudio = magic.EarthExecutor.GetComponent<EarthAudioDirector>(); hud = One<EarthDuelHud>();
            disableNewCosmetics = Environment.GetCommandLineArgs().Contains("--perf-disable-new-cosmetics");
            measureAllocations=Environment.GetCommandLineArgs().Contains("--perf-scoped-allocations");
            nativeGc=Environment.GetCommandLineArgs().Contains("--perf-native-gc");
            if(nativeGc&&measureAllocations){Fail("Use --perf-native-gc separately from --perf-scoped-allocations and timing A/B.");yield break;}
            if(nativeGc&&disableNewCosmetics){Fail("Native active-effect coverage requires cosmetics enabled; use the separate timing OFF/ON runs for A/B.");yield break;}
            if(nativeGc)Application.targetFrameRate=60; // Diagnostic-only bound; timing A/B stays uncapped.
            HardPolishAllocationCounters.Reset();
            if (gold == null || localAudio == null || camera == null) { Fail("Resolve the existing bound Gold/audio/output-camera owners before QA."); yield break; }
            bot = duel.BotTransform.GetComponent<EarthMvpBotController>();
            double end = Time.realtimeSinceStartupAsDouble + 150;
            while ((!ready.IsReady || !fire.IsReady || flow.State == FrontendState.Loading) && !ready.Failed && fire.Failure == null && Time.realtimeSinceStartupAsDouble < end) yield return null;
            if (!ready.IsReady || !fire.IsReady || flow.State != FrontendState.Main) { Fail("Production readiness failed or timed out."); yield break; }
            var pipeline = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline == null || Mathf.Abs(pipeline.renderScale - 1) > .001f || camera.allowDynamicResolution || camera.pixelWidth != 1920 || camera.pixelHeight != 1080)
            { Fail("1080p gate requires production URP renderScale=1 and no dynamic-resolution camera."); yield break; }
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            structures = roots.SelectMany(r => r.GetComponentsInChildren<EarthArenaStructure>(true)).Where(x => x.isActiveAndEnabled && x.OrdinaryDamageEnabled).OrderBy(x => Hierarchy(x.transform)).ToArray();
            decor = roots.SelectMany(r => r.GetComponentsInChildren<EarthDestructibleDecorRock>(true)).Where(x => x.isActiveAndEnabled && x.IsEarthTargetValid).OrderBy(x => Hierarchy(x.transform)).ToArray();
            report = new Report { scenario = Scenario, revision = revision, buildGuid = Application.buildGUID,
                cosmeticMode = disableNewCosmetics ? "disabled" : "enabled",
                cosmeticScope = "playable Fire render publication; Gold respawn; Main/results stage; G13 extra sparks/audio/icon motion. Geometry, shaders, gameplay, poses, camera, wind, birds, fog and legacy material/audio response are identical.", utc = DateTime.UtcNow.ToString("O"),
                platform = Application.platform.ToString(), api = SystemInfo.graphicsDeviceType.ToString(), gpu = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType, quality = QualitySettings.names[QualitySettings.GetQualityLevel()], unity = Application.unityVersion,
                renderer = pipeline.name, sceneManifest = Argument("--perf-manifest", "UNAVAILABLE"), width = Screen.width, height = Screen.height,
                frameCap = Application.targetFrameRate, vSync = QualitySettings.vSyncCount, seed = Seed, renderScale = pipeline.renderScale,
                standalone = !Application.isEditor, nativeGcCapture=nativeGc, scopedAllocationMeasurement=measureAllocations, frameCapacity = Capacity, destructionTargets = structures.Length + decor.Length,
                dayTime = One<CelestialSystemBehaviour>().Snapshot.TimeOfDay01 };
            if (File.Exists(report.sceneManifest)) File.Copy(report.sceneManifest, Path.Combine(output, "scene-profile-manifest.json"), true);
            if(!nativeGc)
            {
            main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            wind = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.VFX.SurfaceWindDust", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            responses = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Response.Audio", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            }
            ApplyCosmeticMode();
            yield return Seconds(12);
            if (!disableNewCosmetics && (stage.Failure != null || !gold.StandingPosesReady))
            { Fail("Enabled cosmetic owners failed to warm; an inactive effect is not valid performance evidence."); yield break; }
            yield return Screenshot("01-main");
            if (!flow.BeginBot()) { Fail("Bot start rejected."); yield break; }
            yield return CombatReady(); if (finished) yield break;
            report.dayTime = One<CelestialSystemBehaviour>().Snapshot.TimeOfDay01;
            if (bot != null) bot.enabled = false;
            adapter.ConfigureRemoteInput(true); yield return Seconds(10);
            yield return Screenshot("02-steady-start");
            BeginPhase(0); yield return Seconds(20); capture = false;
            yield return Screenshot("03-steady-end");
            if (bot != null) bot.enabled = true;
            observedBotStrikes=0;lastBotStrikeCount=bot!=null?bot.StrikeCount:0;observeBotStrikes=true;
            replay = true; combatStart = Time.realtimeSinceStartupAsDouble; BeginPhase(1);
            yield return Seconds(120); capture = replay = false;
            ObserveBotStrikes();observeBotStrikes=false;report.botSpells=observedBotStrikes;
            report.fireBegins = fire.PoseBegins;
            fire.PlayerSession.Stop(); fire.BotSession.Stop(); adapter.ConfigureRemoteInput(true);
            if (bot != null) bot.enabled = false;
            report.releasedPieces = structures.Sum(s => s != null ? s.ReleasedPieceCount : 0);
            if (report.destructionTargets == 0 || report.releasedPieces == 0 || destructionIndex != report.destructionTargets)
            { Fail("Heavy destruction did not exhaust its target schedule or release structural pieces; scenario is not valid."); yield break; }
            if (report.botSpells == 0 || report.fireBegins == 0) { Fail("The combat sample did not exercise both bot Earth and playable Fire."); yield break; }
            if (!disableNewCosmetics && (feedback.PolishSparkEvents == 0 || localAudio.ResponseAudioEvents == 0))
            { Fail("Enabled G13 effects produced no events; verify explicit response bindings before measuring."); yield break; }
            yield return Screenshot("04-heavy-start"); BeginPhase(2); yield return Seconds(20); capture = false;
            yield return Screenshot("05-heavy-end"); BeginPhase(3);
            for (int i = 0; i < 10; i++)
            {
                if(nativeGc&&(i==0||i==3||i==6||i==9))
                {
                    while(nativeCapture!=null&&!finished)yield return null;
                    StartNativeWindow(7+i/3,"transition-"+i);
                    while(nativeCapture!=null&&nativeFrames<3&&!finished)yield return null;
                }
                flow.EndMatch(); yield return null;
                double restoreDeadline=Time.realtimeSinceStartupAsDouble+150;
                while(flow.State==FrontendState.Main&&!flow.IsWorldReady&&duel.ArenaResetError==null&&Time.realtimeSinceStartupAsDouble<restoreDeadline)yield return null;
                if (flow.State != FrontendState.Main || !flow.IsWorldReady || !flow.BeginBot()) { Fail("Transition cycle failed."); yield break; }
                yield return CombatReady(); if (finished) yield break;
                if (bot != null) bot.enabled = false;
                report.transitions++; yield return Seconds(.25f);
            }
            if(nativeGc)
            {
                while(nativeCapture!=null&&!finished)yield return null;
                yield return CaptureNativeActiveBranches();
                if(finished)yield break;
            }
            capture = false; yield return Screenshot("06-transitions-end");
            WriteResults(); finished = true; Application.Quit(0);
        }
        private IEnumerator CombatReady()
        {
            double end = Time.realtimeSinceStartupAsDouble + 60;
            while (flow.State != FrontendState.Combat && Time.realtimeSinceStartupAsDouble < end) yield return null;
            if (flow.State != FrontendState.Combat) Fail("Combat readiness timeout.");
        }
        private IEnumerator Seconds(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (!finished && Time.realtimeSinceStartupAsDouble < end) yield return null; }
        private void BeginPhase(int value) { HardPolishAllocationCounters.SetWindow(false,value); ApplyCosmeticMode(); phase = value; skipFrames = 8; capture = true; }
        private void ApplyCosmeticMode()
        {
            bool enabled = !disableNewCosmetics;
            fire.SetCosmeticRenderingForQa(enabled); gold.enabled = enabled; stage.enabled = enabled;
            feedback.SetCosmeticsForQa(enabled); localAudio.SetCosmeticsForQa(enabled); hud.SetSchoolCosmeticsForQa(enabled);
        }
        private void Update()
        {
            if (!replay || finished) return;
            double age = Time.realtimeSinceStartupAsDouble - combatStart;
            if (age >= 60) destroying = true;
            if (destroying && destructionIndex < structures.Length + decor.Length && destructionIndex <= (age - 60) * 10)
            {
                int index = destructionIndex++;
                if (index < structures.Length)
                { var s = structures[index]; s.SetMagicDisassemblyProgress(1, s.transform.position, s.transform.up); }
                else { var rock = decor[index - structures.Length]; if (rock != null) rock.ApplyImpact(rock.transform.position, rock.transform.up, 1000000); }
            }
            bool playable = duel.CanReceiveDamage(Elemental.Simulation.Combat.EarthDuelFighterId.Player) && magic.isActiveAndEnabled;
            if (!playable) { wasPlayable = false; return; }
            if (!wasPlayable) { adapter.ConfigureRemoteInput(true); previousHeld = 0; wasPlayable = true; }
            double cycle = age % 12;
            ElementId school = cycle < 6 ? ElementId.Earth : ElementId.Fire;
            if (!schoolRequested || lastRequestedSchool != school)
            { lastRequestedSchool = school; schoolRequested = true; if (magic.SelectedElement != school) magic.TrySelectElement(school); }
            EarthInputBits held = cycle % 6 > 1 && cycle % 6 < 3 ? EarthInputBits.Primary : EarthInputBits.None;
            Vector3 target = camera.WorldToViewportPoint(duel.BotTransform.position + duel.BotTransform.up);
            var input = new EarthSemanticInputFrame { Sequence = ++sequence, Tick = sequence,
                Held = held, Pressed = held & ~previousHeld, Released = previousHeld & ~held,
                PointerViewport = new float2(Mathf.Clamp01(target.x), Mathf.Clamp01(target.y)),
                Move = new float2(Mathf.Sin((float)age * .5f) * .35f, .2f), FieldOfView = Mathf.Clamp(camera.fieldOfView, 20, 110),
                Aspect = camera.aspect, CameraPosition = camera.transform.position, CameraRotation = camera.transform.rotation };
            adapter.EnqueueRemoteInput(in input); previousHeld = held;
        }
        private void ObserveBotStrikes()
        {
            if(!observeBotStrikes||bot==null)return;
            int current=bot.StrikeCount;
            // Admission increments this counter; phase events precede the admission guard.
            // ResetPlanner clears it on each life. This accumulated observation is a
            // conservative lower bound if reset and another strike share a frame.
            observedBotStrikes+=current>=lastBotStrikeCount?current-lastBotStrikeCount:current;
            lastBotStrikeCount=current;
        }
        private void LateUpdate()
        {
            ObserveBotStrikes();
            if(nativeGc)
            {
                // Recorder/callstack overhead is never exported as an A/B timing member.
                bool eligibleNative=capture&&skipFrames--<=0&&!finished;
                try{TickNativeCapture(eligibleNative);}
                catch(Exception error){Fail("Native GC capture failed: "+error);}
                return;
            }
            FrameTimingManager.CaptureFrameTimings();
            uint available = FrameTimingManager.GetLatestTimings(1, timing);
            double gpu = -1;
            if (available > 0 && timing[0].frameStartTimestamp > lastGpuTimestamp)
            { lastGpuTimestamp = timing[0].frameStartTimestamp; if (double.IsFinite(timing[0].gpuFrameTime) && timing[0].gpuFrameTime > 0) gpu = timing[0].gpuFrameTime; }
            bool eligible=capture&&skipFrames--<=0;
            HardPolishAllocationCounters.SetWindow(measureAllocations&&eligible,phase);
            if (!eligible) return;
            if (count == Capacity) { report.captureOverflow = true; return; }
            frames[count++] = new Frame { phase = phase, time = Time.realtimeSinceStartupAsDouble,
                main = PositiveThread(main), render = PositiveThread(render), gpu = gpu,
                gc = Value(gc, 1), wind = Value(wind, .000001), responses = Value(responses, .000001),
                cameraPosition = camera.transform.position, cameraRotation = camera.transform.rotation, fieldOfView = camera.fieldOfView };
        }
        private IEnumerator CaptureNativeActiveBranches()
        {
            // Native-only diagnostic suffix. Ordinary authoritative damage produces
            // the actual KO/return; no Gold owner method or cue is manufactured.
            if(bot!=null)bot.enabled=false;
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while((!duel.CanReceiveDamage(EarthDuelFighterId.Player)||!duel.CanReceiveDamage(EarthDuelFighterId.Bot))&&
                Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var handoff=RagdollHandoff.Uniform(Vector3.zero);
            if(!duel.ApplyDamage(EarthDuelFighterId.Player,duel.MaximumHealth+1,in handoff)||
                !duel.ApplyDamage(EarthDuelFighterId.Bot,duel.MaximumHealth+1,in handoff))
            {Fail("Native Gold coverage requires both actual authoritative knockouts.");yield break;}
            deadline=Time.realtimeSinceStartupAsDouble+8;
            while(duel.PlayerRespawnRemaining>1.05f&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(duel.PlayerRespawnRemaining<=0){Fail("Missed the actual Gold return window.");yield break;}
            StartNativeWindow(11,"gold-authoritative-return");
            while(nativeCapture!=null&&!finished)yield return null;
            // There is no public local time-expiry shortcut. Exercise the ordinary
            // authoritative replica snapshot path, as the production result fixture
            // does, explicitly labelled here rather than overriding a VFX owner.
            nativePriorAuthority=duel.HasSimulationAuthority;nativeReplicaAuthorityOwned=true;
            try
            {
                duel.ConfigureOnlineAuthority(false);
                for(int outcome=0;outcome<3&&!finished;outcome++)
                {
                    if(!duel.ApplyReplicaMatch(duel.MaximumHealth,duel.MaximumHealth,0,0,60,false))
                    {Fail("Replica result reset rejected.");yield break;}
                    yield return null;yield return new WaitForEndOfFrame();
                    string label=outcome==0?"results-replica-victory":outcome==1?"results-replica-defeat":"results-replica-draw";
                    StartNativeWindow(12+outcome,label);
                    while(nativeCapture!=null&&nativeFrames<3&&!finished)yield return null;
                    int playerScore=outcome==1?1:3,botScore=outcome==0?1:3;
                    if(!duel.ApplyReplicaMatch(duel.MaximumHealth,duel.MaximumHealth,playerScore,botScore,0,true))
                    {Fail("Authoritative replica result snapshot rejected.");yield break;}
                    while(nativeCapture!=null&&!finished)yield return null;
                }
            }
            finally{RestoreNativeAuthority();}
        }
        private void RestoreNativeAuthority()
        {
            if(!nativeReplicaAuthorityOwned)return;nativeReplicaAuthorityOwned=false;
            if(duel!=null)duel.ConfigureOnlineAuthority(nativePriorAuthority);
        }
        private void StartNativeWindow(int request,string reason)
        {
            if(!nativeGc||nativeCapture!=null||nativeRequests[request]||finished)return;
            nativeRequests[request]=true;
            string folder=Path.Combine(output,"NativeGC");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,request.ToString("00",CultureInfo.InvariantCulture)+"-"+reason+".raw");
            nativeCapture=new HardPolishNativeGcCapture(path);nativeFrames=0;
            nativeFile=new NativeGcFile{path=path,reason=reason,phase=phase,firstEngineFrame=Time.frameCount,
                started=Time.realtimeSinceStartupAsDouble,status="OPEN_UNANALYZED"};
            nativeFiles.Add(nativeFile);
        }
        private void TickNativeCapture(bool eligible)
        {
            if(nativeCapture==null&&eligible)
            {
                if(phase==0)StartNativeWindow(0,"steady");
                else if(phase==2)StartNativeWindow(6,"heavy-settling");
                else if(phase==1)
                {
                    double age=Time.realtimeSinceStartupAsDouble-combatStart;
                    // Four separate existing12s cycles target school, begin, sustained
                    // hold and release. At diagnostic60fps each file spans <=120 frames.
                    if(age>=5.75&&!nativeRequests[1])StartNativeWindow(1,"combat-school");
                    else if(age>=18.75&&!nativeRequests[2])StartNativeWindow(2,"combat-begin");
                    else if(age>=31.65&&!nativeRequests[3])StartNativeWindow(3,"combat-hold");
                    else if(age>=44.75&&!nativeRequests[4])StartNativeWindow(4,"combat-release");
                    else if(age>=60.1&&!nativeRequests[5])StartNativeWindow(5,"combat-destruction");
                }
            }
            if(nativeCapture==null){HardPolishAllocationCounters.SetWindow(false,phase);return;}
            nativeFrames++;
            if(nativeFrames==3)
            {
                HardPolishNativeGcCapture.Calibrate(false);nativeFile.startCalibration=true;
                nativeGoldCues=gold.UniqueCueCount;nativeGoldCompleted=gold.CompletedCueCount;
                nativeFireBegins=fire.PoseBegins;nativeSparks=feedback.PolishSparkEvents;
                nativeAudio=localAudio.ResponseAudioEvents;nativeResultGeneration=stage.ResultGeneration;
            }
            if(nativeFrames>3&&nativeFrames<=117)
            {
                nativeFile.measuredFrames++;
                nativeFile.peakGoldEffects=Mathf.Max(nativeFile.peakGoldEffects,gold.ActiveEffectCount);
                nativeFile.peakGoldVisible=Mathf.Max(nativeFile.peakGoldVisible,gold.VisibleProxyCount);
                nativeFile.peakStageStones=Mathf.Max(nativeFile.peakStageStones,stage.ActiveStoneCount);
                nativeFile.peakFireViews=Mathf.Max(nativeFile.peakFireViews,fire.ActiveViews);
                nativeFile.peakFireDraining=Mathf.Max(nativeFile.peakFireDraining,fire.DrainingViews);
                if(gold.ActiveEffectCount>0)nativeFile.goldActiveFrames++;
                if(gold.VisibleProxyCount>0)nativeFile.goldVisibleFrames++;
                if(stage.ActiveStoneCount>0)
                {
                    nativeFile.stageStoneFrames++;
                    if(stage.ResultsActive)nativeFile.stageResultFrames++;
                    else if(flow.State==FrontendState.Main)nativeFile.stageMainFrames++;
                }
                if(fire.ActiveViews>0)nativeFile.fireActiveFrames++;
                if(fire.DrainingViews>0)nativeFile.fireDrainingFrames++;
            }
            if(nativeFrames==117)
            {
                HardPolishAllocationCounters.SetWindow(false,phase);
                nativeFile.goldCueDelta=gold.UniqueCueCount-nativeGoldCues;
                nativeFile.goldCompletedDelta=gold.CompletedCueCount-nativeGoldCompleted;
                nativeFile.fireBeginDelta=fire.PoseBegins-nativeFireBegins;
                nativeFile.sparkCueDelta=feedback.PolishSparkEvents-nativeSparks;
                nativeFile.audioCueDelta=localAudio.ResponseAudioEvents-nativeAudio;
                nativeFile.resultGenerationDelta=stage.ResultGeneration-nativeResultGeneration;
                HardPolishNativeGcCapture.Calibrate(true);nativeFile.endCalibration=true;
            }
            HardPolishAllocationCounters.SetWindow(nativeFrames>=3&&nativeFrames<117&&!finished,phase);
            if(nativeFrames>=120)CloseNativeWindow(false);
        }
        private void CloseNativeWindow(bool aborted)
        {
            HardPolishAllocationCounters.SetWindow(false,phase);
            if(nativeCapture==null)return;
            try{nativeCapture.Dispose();}
            finally
            {
                nativeCapture=null;
                nativeFile.lastEngineFrame=Time.frameCount;nativeFile.ended=Time.realtimeSinceStartupAsDouble;
                nativeFile.status=aborted?"ABORTED_INCOMPLETE":"CLOSED_REQUIRES_NATIVE_ANALYSIS";
                if(output!=null)File.WriteAllText(Path.Combine(output,"native-gc-progress.json"),
                    JsonUtility.ToJson(new NativeGcProgress{files=nativeFiles.ToArray()},true));
            }
        }
        [Serializable] private sealed class NativeGcProgress{public NativeGcFile[] files;}
        private static double Value(ProfilerRecorder recorder, double scale) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue * scale : -1;
        private static double PositiveThread(ProfilerRecorder recorder)
        { double value = Value(recorder, .000001); return value > 0 ? value : -1; }
        private IEnumerator Screenshot(string name)
        {
            HardPolishAllocationCounters.SetWindow(false,phase);
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); Destroy(image);
        }
        private void WriteResults()
        {
            CloseNativeWindow(true);
            HardPolishAllocationCounters.SetWindow(false,phase);
            if(nativeGc)
            {
                if(nativeFiles.Count!=15||nativeFiles.Any(f=>!f.startCalibration||!f.endCalibration||f.status!="CLOSED_REQUIRES_NATIVE_ANALYSIS"))
                    throw new InvalidOperationException("Native GC schedule did not complete all15 bounded calibrated files.");
                if(!nativeFiles.Any(f=>f.goldActiveFrames>0&&f.goldVisibleFrames>0&&f.goldCompletedDelta>0)||
                    !nativeFiles.Any(f=>f.fireActiveFrames>0&&f.fireBeginDelta>0)||!nativeFiles.Any(f=>f.fireDrainingFrames>0)||
                    !nativeFiles.Any(f=>f.stageMainFrames>0)||nativeFiles.Count(f=>f.stageResultFrames>0&&f.resultGenerationDelta>0)<3||
                    !nativeFiles.Any(f=>f.sparkCueDelta>0&&f.audioCueDelta>0))
                    throw new InvalidOperationException("Native windows missed active Gold, Main/results, Fire hold/tail or G13 cues; callback calls alone are insufficient coverage.");
                report.nativeGcFiles=nativeFiles.ToArray();report.phases=Array.Empty<Phase>();
                report.allocationCounterStatus=HardPolishAllocationCounters.CounterStatus;
                report.allocationCounterCalibrationBytes=HardPolishAllocationCounters.CalibrationBytes;
                report.allocationCounterSmallCalibrationBytes=HardPolishAllocationCounters.SmallCalibrationBytes;
                report.scopedAllocations=HardPolishAllocationCounters.Snapshot();
                report.status="NATIVE GC DIAGNOSTIC ONLY; not a timing A/B member. Raw files require calibrated Editor analysis; absent marker coverage is UNEXERCISED, never zero.";
                File.WriteAllText(Path.Combine(output,"native-gc.json"),JsonUtility.ToJson(report,true));
                return;
            }
            report.allocationCounterStatus=measureAllocations?HardPolishAllocationCounters.CounterStatus:"NOT_REQUESTED";
            report.allocationCounterSmallCalibrationBytes=measureAllocations?HardPolishAllocationCounters.SmallCalibrationBytes:-1;
            report.allocationCounterCalibrationBytes=measureAllocations?HardPolishAllocationCounters.CalibrationBytes:-1;
            report.scopedAllocations=measureAllocations?HardPolishAllocationCounters.Snapshot():Array.Empty<HardPolishAllocationCounters.Sample>();
            var csv = new System.Text.StringBuilder("phase,time,main_ms,render_ms,gpu_ms,gc_bytes,wind_ms,response_audio_ms,camera_x,camera_y,camera_z,camera_qx,camera_qy,camera_qz,camera_qw,fov\n");
            for (int i = 0; i < count; i++) { var f = frames[i]; csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R},{14:R},{15:R}\n", phases[f.phase],f.time,f.main,f.render,f.gpu,f.gc,f.wind,f.responses,f.cameraPosition.x,f.cameraPosition.y,f.cameraPosition.z,f.cameraRotation.x,f.cameraRotation.y,f.cameraRotation.z,f.cameraRotation.w,f.fieldOfView); }
            File.WriteAllText(Path.Combine(output, "frames.csv"), csv.ToString());
            report.phases = new Phase[4];
            for (int p = 0; p < 4; p++)
            {
                var metrics = new Metric[6];
                for (int m = 0; m < 6; m++)
                {
                    int n = 0;
                    for (int i = 0; i < count; i++) if (frames[i].phase == p)
                    { var f = frames[i]; double v = m == 0 ? f.main : m == 1 ? f.render : m == 2 ? f.gpu : m == 3 ? f.gc : m == 4 ? f.wind : f.responses; if (v >= 0) scratch[n++] = v; }
                    // Reuse the repository's quantile definition. This allocation/sort occurs after recording stops.
                    var sorted = new double[n]; Array.Copy(scratch, sorted, n);
                    EarthPercentiles q = EarthPerformanceStatistics.Compute(sorted, n, 0, scratch);
                    metrics[m] = new Metric { name = new[] { "main", "render", "gpu", "gc", "wind", "responseAudio" }[m], unit = m == 3 ? "bytes" : "ms", samples = n,
                        p50 = n > 0 ? q.P50 : -1, p95 = n > 0 ? q.P95 : -1, p99 = n > 0 ? q.P99 : -1, maximum = n > 0 ? q.Maximum : -1 };
                }
                int phaseIndex = p; report.phases[p] = new Phase { name = phases[p], frames = Enumerable.Range(0, count).Count(i => frames[i].phase == phaseIndex), metrics = metrics };
            }
            report.status = "MEASURED SAME-BUILD COSMETIC A/B MEMBER: compare disabled then enabled from this exact build. Not an old-commit baseline. Scoped allocation results apply only to named exercised synchronous callbacks; review coverage and nonzero calls separately.";
            File.WriteAllText(Path.Combine(output, "performance.json"), JsonUtility.ToJson(report, true));
        }
        private static string Hierarchy(Transform value) => value.parent == null ? value.name : Hierarchy(value.parent) + "/" + value.name;
        private void Fail(string message)
        {
            capture = false; finished = true;
            try { CloseNativeWindow(true); }
            catch (Exception error) { Debug.LogException(error); }
            try { RestoreNativeAuthority(); }
            catch (Exception error) { Debug.LogException(error); }
            HardPolishAllocationCounters.SetWindow(false,phase);
            Debug.LogError("Hard polish performance: " + message);
            try { if (output != null) File.WriteAllText(Path.Combine(output,"FAILED.txt"),message); }
            catch (Exception error) { Debug.LogException(error); }
            finally { Application.Quit(2); }
        }
        private void OnDestroy()
        { CloseNativeWindow(true); RestoreNativeAuthority(); HardPolishAllocationCounters.SetWindow(false,phase); if (main.Valid) main.Dispose(); if (render.Valid) render.Dispose(); if (gc.Valid) gc.Dispose(); if (wind.Valid) wind.Dispose(); if (responses.Valid) responses.Dispose(); }
    }
}
