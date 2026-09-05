// Explicit Editor QA harness. Copy into an Editor assembly only after current tests finish.
// Nothing runs until RunProductionStartupSample(label) is invoked. Never saves a scene.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Diagnostics;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Structures;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    [InitializeOnLoad]
    public static class ProductionStartupSample
    {
        private const string ArmedKey = "Elemental.StartupSample.Label";
        private const string EnterKey = "Elemental.StartupSample.EnterTime";
        private const string RestoreKey = "Elemental.StartupSample.CacheRestore";
        private const string InvocationKey = "Elemental.StartupSample.Invocation";
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private static VoxelPlanetBehaviour _planet;
        private static EarthRockDebrisPool _pool;
        private static EarthSceneReadinessGate _gate;
        private static EarthArenaStructure _column;
        private static Transform[] _pieces;
        private static EarthPieceDefinition[] _definitions;
        private static EarthBondDefinition[] _bonds;
        private static readonly List<double> StartupFrames = new();
        private static readonly List<double> ImpactFrames = new();
        private static readonly Dictionary<EarthMvpBotController, bool> Bots = new();
        private static Report _report;
        private static bool _running;
        private static int _lastFrame = -1, _readyFrame, _impactFrame, _releasedIndex = -1;
        private static double _lastRealtime, _finishRequestedAt;
        private static float _impactTime;
        private static string _folder;

        [Serializable]
        private sealed class CacheRestore
        {
            public string label, scene, sceneSha256, planetObjectId, poolObjectId;
            public string baseAssetId, fractureAssetId, baseAssetPath, fractureAssetPath;
        }

        [Serializable]
        public sealed class Report
        {
            public string label, utc, comparisonScope, scene, status, error, unityVersion;
            public bool editorSample, cacheUsed, geometryReady, physicsPrepared, primaryAccepted, secondaryAccepted;
            public string baseCacheStatus, column, readyScreenshot, impactScreenshot, synchronousStartupTiming;
            public double enterToReadyMs, gateAwakeToReadyMs, hydrationMs, peakCookingSliceMs;
            public double bakedCacheLoadMs, backgroundCookingWallMs;
            public int bakedPlans, preparedPlansAtReady, cookedMeshes, scheduledCookedMeshes;
            public int bakedPlanMissesAtReady, runtimeChunks, pendingRender, pendingCollider;
            public int preparationDeltaPrimary, preparationDeltaSecondary, releasedPieces, shatteredPieces;
            public int secondaryExpectedPhysicalPieces;
            public int primaryTargetIndex, secondaryTargetIndex;
            public string primaryTargetName, secondaryTargetName;
            public Vector3 primaryPoint, secondaryPoint;
            public double primaryCallMs, secondaryCallMs;
            public long primaryAllocatedBytes, secondaryAllocatedBytes;
            public int startupObservedFrames, impactObservedFrames;
            public double startupMaxMs, startupP95Ms, impactMaxMs, impactP95Ms;
            public double[] startupFrameMs, impactFrameMs;
            public bool screenshotsExist;
            public string invocation, restorationError;
            public bool transientCacheOverride, cacheReferencesRestored, sceneFileUnchanged, sceneDirtyAfterRestore;
        }

        static ProductionStartupSample()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update += Tick;
        }

        [MenuItem("Elemental/QA/Measure Production Startup Cached")]
        public static void RunCachedFromMenu() => RunFromMenu(false);

        [MenuItem("Elemental/QA/Measure Production Startup Uncached")]
        public static void RunUncachedFromMenu() => RunFromMenu(true);

        [MenuItem("Elemental/QA/Restore Startup Sample Cache References")]
        public static void RestoreFromMenu() => RestoreTransientCaches();

        private static void RunFromMenu(bool uncached)
        {
            Scene scene = ValidateSavedCleanScene();
            if (!string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")))
                throw new InvalidOperationException("An earlier transient cache override needs restoration. Use Elemental/QA/Restore Startup Sample Cache References first.");
            VoxelPlanetBehaviour planet = null;
            EarthRockDebrisPool pool = null;
            int planets = 0, pools = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<VoxelPlanetBehaviour>(true)) { planet = candidate; planets++; }
                foreach (var candidate in root.GetComponentsInChildren<EarthRockDebrisPool>(true)) { pool = candidate; pools++; }
            }
            if (planets != 1 || pools != 1)
                throw new InvalidOperationException("Startup A/B requires exactly one planet and one debris pool in EarthCoreSlice.");
            FieldInfo baseField = CacheField(typeof(VoxelPlanetBehaviour), "baseMeshCache");
            FieldInfo fractureField = CacheField(typeof(EarthRockDebrisPool), "bakedFractureCache");
            var baseAsset = baseField.GetValue(planet) as UnityEngine.Object;
            var fractureAsset = fractureField.GetValue(pool) as UnityEngine.Object;
            if (baseAsset == null || fractureAsset == null)
                throw new InvalidOperationException("Both saved startup caches must be assigned before menu A/B. The uncached menu temporarily clears and restores them.");
            string label = (uncached ? "uncached-menu-" : "cached-menu-") +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            SessionState.SetString(InvocationKey, uncached ? "Menu: uncached transient references" : "Menu: cached saved references");
            if (uncached)
            {
                var snapshot = new CacheRestore { label = label, scene = scene.path,
                    sceneSha256 = SceneHash(scene.path),
                    planetObjectId = GlobalObjectId.GetGlobalObjectIdSlow(planet).ToString(),
                    poolObjectId = GlobalObjectId.GetGlobalObjectIdSlow(pool).ToString(),
                    baseAssetId = GlobalObjectId.GetGlobalObjectIdSlow(baseAsset).ToString(),
                    fractureAssetId = GlobalObjectId.GetGlobalObjectIdSlow(fractureAsset).ToString(),
                    baseAssetPath = AssetDatabase.GetAssetPath(baseAsset),
                    fractureAssetPath = AssetDatabase.GetAssetPath(fractureAsset) };
                SessionState.SetString(RestoreKey, JsonUtility.ToJson(snapshot));
                // Transient reflection assignment intentionally uses no Undo,
                // SetDirty, MarkSceneDirty or SaveScene. Play clones current state.
                baseField.SetValue(planet, null);
                fractureField.SetValue(pool, null);
            }
            try { RunProductionStartupSample(label); }
            catch
            {
                if (uncached && !EditorApplication.isPlayingOrWillChangePlaymode) RestoreTransientCaches();
                SessionState.EraseString(ArmedKey);
                SessionState.EraseString(InvocationKey);
                throw;
            }
        }

        private static Scene ValidateSavedCleanScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before arming a startup sample.");
            if (!string.IsNullOrEmpty(SessionState.GetString(ArmedKey, "")))
                throw new InvalidOperationException("A startup sample is already armed; do not replay its entry command.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || !scene.isLoaded)
                throw new InvalidOperationException("Open saved EarthCoreSlice first. This harness never opens or saves scenes.");
            if (scene.isDirty)
                throw new InvalidOperationException("EarthCoreSlice has unsaved changes. Review/save them before sampling; the harness never clears dirty state.");
            return scene;
        }

        private static FieldInfo CacheField(Type type, string name) =>
            type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(type.FullName, name);

        private static UnityEngine.Object ResolveExactObject(string serializedId)
        {
            if (!GlobalObjectId.TryParse(serializedId, out GlobalObjectId id))
                throw new InvalidOperationException("Invalid saved object identity in startup cache restoration.");
            var value = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            if (value == null) throw new InvalidOperationException("The original startup object/asset is unavailable; restoration record retained: " + serializedId);
            return value;
        }

        private static string SceneHash(string scenePath)
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenePath);
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static void RestoreTransientCaches()
        {
            string json = SessionState.GetString(RestoreKey, "");
            if (string.IsNullOrEmpty(json)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cache restoration waits for EnteredEditMode.");
            CacheRestore snapshot = JsonUtility.FromJson<CacheRestore>(json);
            Scene scene = SceneManager.GetSceneByPath(snapshot.scene);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Original EarthCoreSlice is not loaded. Reopen it and use Restore Startup Sample Cache References; no reference record was discarded.");
            var planet = ResolveExactObject(snapshot.planetObjectId) as VoxelPlanetBehaviour;
            var pool = ResolveExactObject(snapshot.poolObjectId) as EarthRockDebrisPool;
            var baseAsset = ResolveExactObject(snapshot.baseAssetId);
            var fractureAsset = ResolveExactObject(snapshot.fractureAssetId);
            if (planet == null || pool == null || planet.gameObject.scene != scene || pool.gameObject.scene != scene)
                throw new InvalidOperationException("Original startup components changed; restoration record retained for review.");
            FieldInfo baseField = CacheField(typeof(VoxelPlanetBehaviour), "baseMeshCache");
            FieldInfo fractureField = CacheField(typeof(EarthRockDebrisPool), "bakedFractureCache");
            baseField.SetValue(planet, baseAsset);
            fractureField.SetValue(pool, fractureAsset);
            bool restored = (baseField.GetValue(planet) as UnityEngine.Object) == baseAsset &&
                (fractureField.GetValue(pool) as UnityEngine.Object) == fractureAsset;
            if (!restored) throw new InvalidOperationException("Exact startup cache reference restoration failed; record retained.");
            bool diskUnchanged = SceneHash(snapshot.scene) == snapshot.sceneSha256;
            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "BuildReports", "EnvironmentAnimationRescue", snapshot.label, "StartupSample.json");
            if (File.Exists(reportPath))
            {
                Report saved = JsonUtility.FromJson<Report>(File.ReadAllText(reportPath));
                saved.cacheReferencesRestored = true;
                saved.sceneFileUnchanged = diskUnchanged;
                saved.sceneDirtyAfterRestore = scene.isDirty;
                if (!diskUnchanged || scene.isDirty)
                    saved.restorationError = "Cache fields restored; scene file or dirty state changed independently. Review before saving or accepting A/B.";
                File.WriteAllText(reportPath, JsonUtility.ToJson(saved, true));
            }
            SessionState.EraseString(RestoreKey);
            UnityEngine.Debug.Log($"[StartupSample] Original cache references restored; diskUnchanged={diskUnchanged}, sceneDirty={scene.isDirty}. Dirty state was not cleared.");
        }

        public static void RunProductionStartupSample(string label)
        {
            ValidateSavedCleanScene();
            string restoreJson = SessionState.GetString(RestoreKey, "");
            if (!string.IsNullOrEmpty(restoreJson) && JsonUtility.FromJson<CacheRestore>(restoreJson).label != label)
                throw new InvalidOperationException("Restore the earlier transient cache override before starting a different sample.");
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Provide a distinct cached/uncached sample label.");
            foreach (char character in label)
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_'))
                    throw new ArgumentException("Use only letters, digits, underscore or hyphen in the sample label.");
            string destination = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BuildReports", "EnvironmentAnimationRescue", label);
            if (Directory.Exists(destination))
                throw new InvalidOperationException("That sample label already has an output directory. Use a new label to keep evidence and prevent stale screenshots.");
            SessionState.SetString(ArmedKey, label);
            SessionState.SetString(EnterKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            // Keep the actual Game view rendering so ScreenCapture records the production camera.
            Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Show();
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                try { RestoreTransientCaches(); }
                catch (Exception error) { UnityEngine.Debug.LogError("[StartupSample] Cache restoration requires attention: " + error); }
                SessionState.EraseString(ArmedKey);
                SessionState.EraseString(InvocationKey);
                return;
            }
            string label = SessionState.GetString(ArmedKey, "");
            if (string.IsNullOrEmpty(label)) return;
            if (state == PlayModeStateChange.ExitingEditMode)
                SessionState.SetString(EnterKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _report = new Report { label = label, utc = DateTime.UtcNow.ToString("O"), scene = ScenePath,
                    editorSample = true, unityVersion = Application.unityVersion, status = "WaitingForReadiness",
                    invocation = SessionState.GetString(InvocationKey, "Legacy/manual entry; inspect command replay before interpreting timings"),
                    transientCacheOverride = !string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")),
                    comparisonScope = "Current-code null-cache versus assigned-cache A/B; not untouched historical baseline. Enter time includes Editor domain reload and scene activation. Frame samples begin after EnteredPlayMode and use Time.unscaledDeltaTime; camera capture occurs outside measured impact window." };
                _folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BuildReports", "EnvironmentAnimationRescue", label);
                Directory.CreateDirectory(_folder);
                StartupFrames.Clear(); ImpactFrames.Clear(); Bots.Clear();
                _planet = null; _pool = null; _gate = null; _column = null; _pieces = null;
                _definitions = null; _bonds = null;
                _lastFrame = -1; _readyFrame = -1; _impactFrame = -1; _releasedIndex = -1;
                _finishRequestedAt = 0; _running = true;
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    _planet ??= root.GetComponentInChildren<VoxelPlanetBehaviour>(true);
                    _pool ??= root.GetComponentInChildren<EarthRockDebrisPool>(true);
                    _gate ??= root.GetComponentInChildren<EarthSceneReadinessGate>(true);
                    if (root.name == "Outer Stone Ring")
                    {
                        var columns = root.GetComponentsInChildren<EarthArenaStructure>(true);
                        Array.Sort(columns, (a,b) => string.CompareOrdinal(a.name,b.name));
                        if (columns.Length > 0) _column = columns[0];
                    }
                }
                if (_planet == null || _pool == null || _gate == null || _column == null)
                    Fail("Missing explicit production planet/pool/gate/Outer Stone Ring. Bake/wire before sampling, including uncached A/B.");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && _running)
                Fail("Sample interrupted before completion.", false);
        }

        private static void Tick()
        {
            if (!_running || !EditorApplication.isPlaying || _report == null) return;
            try
            {
                double entered = double.Parse(SessionState.GetString(EnterKey,"0"), System.Globalization.CultureInfo.InvariantCulture);
                if (EditorApplication.timeSinceStartup - entered > 180) { Fail("QA sample timed out after 180 seconds."); return; }
                if (_gate.Failed) { Fail("Readiness gate failed: "+_gate.Status); return; }
                if (Time.frameCount == _lastFrame) return;
                _lastFrame = Time.frameCount;
                _lastRealtime = Time.realtimeSinceStartupAsDouble;
                if (_readyFrame < 0)
                {
                    StartupFrames.Add(Time.unscaledDeltaTime * 1000.0);
                    if (!_gate.IsReady) return;
                    _readyFrame = Time.frameCount;
                    _report.enterToReadyMs = (EditorApplication.timeSinceStartup-entered)*1000;
                    _report.gateAwakeToReadyMs = _gate.ReadyMilliseconds;
                    _report.hydrationMs = _planet.BaseCacheHydrateMilliseconds;
                    _report.cacheUsed = _planet.BaseCacheUsed; _report.baseCacheStatus = _planet.BaseCacheStatus;
                    _report.geometryReady = _planet.GeometryReady; _report.physicsPrepared = _pool.PhysicsPrepared;
                    _report.bakedPlans = _pool.BakedFracturePlanCount; _report.preparedPlansAtReady = _pool.PreparedFracturePlanCount;
                    _report.cookedMeshes = _pool.CookedBakedMeshCount; _report.peakCookingSliceMs = _pool.PeakStartupCookingMilliseconds;
                    _report.scheduledCookedMeshes = _pool.ScheduledBakedMeshCount;
                    _report.bakedCacheLoadMs = _pool.BakedCacheLoadMilliseconds;
                    _report.backgroundCookingWallMs = _pool.BackgroundCookingWallMilliseconds;
                    _report.bakedPlanMissesAtReady = _pool.BakedFracturePlanMissCount;
                    _report.synchronousStartupTiming = EarthStartupTiming.Format();
                    _report.runtimeChunks = _planet.RuntimeChunkCount; _report.pendingRender = _planet.PendingRenderCount; _report.pendingCollider = _planet.PendingColliderCount;
                    if (_report.transientCacheOverride && (_report.cacheUsed || _report.bakedPlans != 0))
                    { Fail("Transient uncached fields were not copied into Play Mode; this is not a valid uncached sample."); return; }
                    if (_report.invocation == "Menu: cached saved references" && (!_report.cacheUsed || _report.bakedPlans == 0))
                    { Fail("Cached menu run did not use both assigned caches; this is not a valid cached sample."); return; }
                    // Normalize only post-ready interference; startup configuration remains untouched.
                    foreach (GameObject root in SceneManager.GetSceneByPath(ScenePath).GetRootGameObjects())
                        foreach (var bot in root.GetComponentsInChildren<EarthMvpBotController>(true))
                        { Bots[bot] = bot.enabled; bot.enabled = false; }
                    _report.readyScreenshot = Path.Combine(_folder,"Ready.png");
                    ScreenCapture.CaptureScreenshot(_report.readyScreenshot);
                    _report.status = "SettlingBeforeFirstImpact";
                    return;
                }
                if (_impactFrame < 0)
                {
                    // Keep ScreenCapture's readback outside the measured interaction window.
                    if (Time.frameCount-_readyFrame < 60) return;
                    _pieces = typeof(EarthArenaStructure).GetField("pieces",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_column) as Transform[];
                    if (_pieces == null || _pieces.Length == 0 || _column.IsFractured) { Fail("First column is missing pieces or was already fractured before the sample."); return; }
                    _definitions = typeof(EarthArenaStructure).GetField("_pieceDefinitions",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_column) as EarthPieceDefinition[];
                    _bonds = typeof(EarthArenaStructure).GetField("_bondDefinitions",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_column) as EarthBondDefinition[];
                    if (_definitions == null || _definitions.Length != _pieces.Length || _bonds == null)
                    { Fail("Missing canonical piece/bond definitions; cannot identify a nonfoundation impact target."); return; }
                    int selected = -1; float largest = -1;
                    Vector3 point = default;
                    for (int i=0; i<_pieces.Length; i++)
                    {
                        if (IsFoundation(i) || !TryDescribeCell(_pieces[i], out Vector3 center,
                                out float volume, out float radius, out float mass)) continue;
                        if (_pool.ResolveBreak(radius, mass, 1000000f).PhysicalPieces <= 0) continue;
                        if (volume > largest) { largest=volume; selected=i; point=center; }
                    }
                    if (selected < 0) { Fail("No nonfoundation column cell can produce physical secondary fragments."); return; }
                    _report.primaryTargetIndex = selected; _report.primaryTargetName = _pieces[selected].name;
                    _report.primaryPoint = point;
                    Vector3 up = (point-_planet.transform.position).normalized;
                    var impact = new EarthStructureImpact(point,up,220f,EarthStructureImpactKind.Projectile,_column.StructureId^0xFF000000u);
                    int prepared = _pool.PreparedFracturePlanCount;
                    long allocation = GC.GetAllocatedBytesForCurrentThread(), timer = Stopwatch.GetTimestamp();
                    bool accepted = _column.ApplyEarthImpact(in impact);
                    _report.primaryCallMs = (Stopwatch.GetTimestamp()-timer)*1000.0/Stopwatch.Frequency;
                    _report.primaryAllocatedBytes = GC.GetAllocatedBytesForCurrentThread()-allocation;
                    _report.primaryAccepted = accepted; _report.column = _column.name;
                    _report.preparationDeltaPrimary = _pool.PreparedFracturePlanCount-prepared;
                    _report.releasedPieces = _column.ReleasedPieceCount;
                    largest=-1;
                    for (int i = _pieces.Length-1; i>=0; i--)
                        if (_column.IsPieceReleased(i) && !IsFoundation(i))
                        {
                            Vector3 size=_pieces[i].GetComponent<Collider>().bounds.size;
                            float volume=size.x*size.y*size.z;
                            float radius=Mathf.Max(.1f,Mathf.Pow(volume*.2387324f,1f/3f));
                            if (_pool.ResolveBreak(radius,_pieces[i].GetComponent<Rigidbody>().mass,1000000f).PhysicalPieces <= 0) continue;
                            if(volume>largest) { largest=volume; _releasedIndex=i; }
                        }
                    _impactFrame = Time.frameCount; _impactTime = Time.time;
                    _report.status = "MeasuringFirstInteraction";
                    if (!accepted || !_column.IsPieceReleased(selected) || _releasedIndex < 0)
                    { Fail("Real ApplyEarthImpact did not release the selected nonfoundation physical-fracture cell."); return; }
                    return;
                }
                if (_finishRequestedAt == 0)
                {
                    ImpactFrames.Add(Time.unscaledDeltaTime*1000.0);
                    if (!_report.secondaryAccepted && _report.secondaryCallMs == 0 && Time.time-_impactTime >= .25f)
                    {
                        if (!_column.IsPieceReleased(_releasedIndex)) { Fail("Selected released cell shattered before the measured secondary call; rerun sample."); return; }
                        if (!TryDescribeCell(_pieces[_releasedIndex], out Vector3 point, out _, out _, out _))
                        { Fail("Released target lost its readable collider geometry before secondary impact."); return; }
                        _report.secondaryTargetIndex = _releasedIndex;
                        _report.secondaryTargetName = _pieces[_releasedIndex].name;
                        _report.secondaryPoint = point;
                        Vector3 size = _pieces[_releasedIndex].GetComponent<Collider>().bounds.size;
                        float radius = Mathf.Max(.1f,Mathf.Pow(size.x*size.y*size.z*.2387324f,1f/3f));
                        _report.secondaryExpectedPhysicalPieces = _pool.ResolveBreak(radius,_pieces[_releasedIndex].GetComponent<Rigidbody>().mass,1000000f).PhysicalPieces;
                        if(_report.secondaryExpectedPhysicalPieces<=0) { Fail("Selected secondary target is dust-only; no physical cache hit was measured. Choose a larger column cell."); return; }
                        var impact = new EarthStructureImpact(point,(point-_planet.transform.position).normalized,1000000f,
                            EarthStructureImpactKind.Projectile,_column.StructureId^0xFE000000u);
                        int prepared = _pool.PreparedFracturePlanCount;
                        long allocation = GC.GetAllocatedBytesForCurrentThread(), timer = Stopwatch.GetTimestamp();
                        _report.secondaryAccepted = _column.ApplyReleasedPieceImpact(_releasedIndex,in impact);
                        _report.secondaryCallMs = (Stopwatch.GetTimestamp()-timer)*1000.0/Stopwatch.Frequency;
                        _report.secondaryAllocatedBytes = GC.GetAllocatedBytesForCurrentThread()-allocation;
                        _report.preparationDeltaSecondary = _pool.PreparedFracturePlanCount-prepared;
                        _report.shatteredPieces = _column.ShatteredPieceCount;
                        if (!_report.secondaryAccepted) { Fail("Real ApplyReleasedPieceImpact rejected the first secondary fracture."); return; }
                    }
                    if (Time.frameCount-_impactFrame < 45 || !_report.secondaryAccepted) return;
                    _report.impactScreenshot = Path.Combine(_folder,"AfterImpact.png");
                    ScreenCapture.CaptureScreenshot(_report.impactScreenshot);
                    _finishRequestedAt = _lastRealtime;
                    _report.status = "WaitingForScreenshots";
                    return;
                }
                bool imagesReady = File.Exists(_report.readyScreenshot) && File.Exists(_report.impactScreenshot);
                if (!imagesReady && _lastRealtime-_finishRequestedAt < 10) return;
                _report.screenshotsExist = imagesReady;
                if (!imagesReady) { Fail("ScreenCapture did not produce both Game-view images; focus the Game view and rerun."); return; }
                _report.status = "Complete"; Finish();
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        private static bool IsFoundation(int index)
        {
            if ((_definitions[index].Flags & EarthPieceFlags.Foundation) != 0) return true;
            foreach (EarthBondDefinition bond in _bonds)
                if (bond.PieceA == index && bond.PieceB == EarthBondGraph.WorldPieceIndex) return true;
            return false;
        }

        private static bool TryDescribeCell(Transform piece, out Vector3 center,
            out float boundsVolume, out float radius, out float mass)
        {
            center=default; boundsVolume=radius=mass=0;
            var collider=piece.GetComponent<MeshCollider>();
            var body=piece.GetComponent<Rigidbody>();
            if (collider == null || body == null || collider.sharedMesh == null || !collider.sharedMesh.isReadable) return false;
            Mesh mesh=collider.sharedMesh;
            Vector3[] vertices=mesh.vertices;
            if(vertices.Length==0) return false;
            foreach(Vector3 vertex in vertices) center+=vertex;
            center=piece.TransformPoint(center/vertices.Length);
            // Dormant Collider.bounds is empty. Transform the actual mesh bounds,
            // including rotation and nonuniform scale, before checking break policy.
            Matrix4x4 matrix=piece.localToWorldMatrix;
            Vector3 e=mesh.bounds.extents;
            Vector3 size=2f*new Vector3(
                Mathf.Abs(matrix.m00)*e.x+Mathf.Abs(matrix.m01)*e.y+Mathf.Abs(matrix.m02)*e.z,
                Mathf.Abs(matrix.m10)*e.x+Mathf.Abs(matrix.m11)*e.y+Mathf.Abs(matrix.m12)*e.z,
                Mathf.Abs(matrix.m20)*e.x+Mathf.Abs(matrix.m21)*e.y+Mathf.Abs(matrix.m22)*e.z);
            boundsVolume=size.x*size.y*size.z;
            radius=Mathf.Max(.1f,Mathf.Pow(boundsVolume*.2387324f,1f/3f));
            mass=body.mass;
            return boundsVolume>0 && float.IsFinite(boundsVolume);
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values.Count==0) return 0;
            var sorted=values.ToArray(); Array.Sort(sorted);
            return sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*percentile)-1)];
        }
        private static void Fail(string message, bool stop=true)
        {
            if (_report==null) return;
            _report.status="Failed"; _report.error=message; UnityEngine.Debug.LogError("[StartupSample] "+message);
            Finish(stop);
        }
        private static void Finish(bool stop=true)
        {
            _running=false;
            _report.startupFrameMs=StartupFrames.ToArray(); _report.impactFrameMs=ImpactFrames.ToArray();
            _report.startupObservedFrames=StartupFrames.Count; _report.impactObservedFrames=ImpactFrames.Count;
            _report.startupMaxMs=Percentile(StartupFrames,1); _report.startupP95Ms=Percentile(StartupFrames,.95);
            _report.impactMaxMs=Percentile(ImpactFrames,1); _report.impactP95Ms=Percentile(ImpactFrames,.95);
            foreach(var pair in Bots) if(pair.Key!=null) pair.Key.enabled=pair.Value;
            Bots.Clear();
            File.WriteAllText(Path.Combine(_folder,"StartupSample.json"),JsonUtility.ToJson(_report,true));
            SessionState.EraseString(ArmedKey); SessionState.EraseString(EnterKey);
            UnityEngine.Debug.Log("[StartupSample] "+_report.status+": "+Path.Combine(_folder,"StartupSample.json"));
            if(stop) EditorApplication.isPlaying=false;
        }
    }
}
