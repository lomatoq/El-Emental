# Bootstrap and inline bevel rescue staging

This directory is intentionally outside `Assets`; Unity does not import it while another QA run owns the Editor.

## Integrate

1. Copy `Runtime/EarthBootstrapSceneLoader.cs` to `Assets/Elemental/Runtime/Bootstrap/`.
2. Replace `Assets/Elemental/Authoring/Build/NativeBuildSceneOrder.cs` with the staged Authoring file.
3. In `ElementalBuildPipeline.NativePlayableScenes`, call:
   `NativeBuildSceneOrder.Create(EnabledScenes(), M0ProjectSetup.BootstrapScenePath, M3EarthCoreSetup.EarthCoreScenePath)`.
4. In `M0ProjectSetup.BuildWindowsSmoke`, pass `BootstrapScenePath` before `EarthCoreScenePath`.
   In `M0ProjectSetup.Configure`, adding/configuring the loader is optional because its runtime initializer attaches it to the Bootstrap scene; explicit scene wiring is still useful for inspector visibility.
5. Copy `Authoring/OuterRingSceneMeshNormalizer.cs` to `Assets/Elemental/Authoring/Editor/`.
6. Replace the existing NativeBuildSceneOrder test and add the persistent-piece test.
7. Change the `EarthArenaStructure.InitializeRuntime` render assignment so bevel generation only runs when `Application.isPlaying`; in edit mode assign `filter.sharedMesh = bakedRenderMesh`. This prevents the importer from reintroducing scene-local meshes.
8. With a clean saved EarthCoreSlice active, run `Elemental/Arena/Remove Serialized Runtime Bevel Duplicates`. It writes a timestamped backup before changing the scene.

## Required validation

- EditMode: NativeBuildSceneOrder tests and `SavedOuterRingPiecesReferencePersistentSourceMeshes` pass.
- Inspect EarthCoreSlice YAML: 85 OuterArch `Beveled Render` inline Mesh blocks are gone; file size should fall by about 37.1 MB.
- Existing OuterStoneRing PlayMode suite remains green, including exact piece picking, all loose rocks, fast impact and repair.
- During Play, all 85 dormant piece filters receive deterministic bevel meshes; source normals and 85-piece count are unchanged.
- Development Player, not Editor Play: first displayed frame is the Bootstrap cover; arena/character never display before planet geometry; cover disappears only after `EarthSceneReadinessGate.IsReady`.
- `-smokeAutoQuit` is handled by the playable scene's WorldBootstrap after readiness; the lightweight Bootstrap clock stays disabled and cannot quit during the load.
- Capture cold Player time to first cover, activation longest frame, and cover-to-ready. The current Editor A/B (12.75 s cached / 43.74 s uncached) remains diagnostic only.

## Fresh Development Player evidence

In the existing Editor, run `Elemental/Build/Build Windows Development From Saved Scenes`.
This entry reads the saved enabled-scene list and does not invoke `M0.Configure`, rewrite
the Bootstrap scene, change graphics APIs, or request a clean build cache. After the
build completes, run this in a separate process; the Editor may remain open:

`powershell -ExecutionPolicy Bypass -File Tools/BootstrapLoadingPatch/Run-FreshPlayerStartup.ps1`

The script never starts Unity or builds the project. It starts the already-built D3D11 Player in a hidden window and
writes `BootstrapStartup.json`, `BootstrapCover.png`, `PlayableReady.png`, and `Player.log`
below `BuildReports/StartupPlayer/<UTC stamp>`. This proves the two rendered states and
records app-uptime milestones. Record whether the Editor remained open alongside the
report. This is a fresh Player-process measurement; do not call it a cold-disk result.
