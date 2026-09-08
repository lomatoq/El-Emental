# Platform contour input repair

Inspection baseline: current main `2f0fbe86`, September 8. User reports that drawn platform contours disappeared and platforms can no longer be created.

## Reproduced configuration defect

The production player has a configured `EarthPreviewPresenter`, but its `LineRenderer` (`587163387` in EarthCoreSlice) is serialized disabled. `Present()` populated vertex positions without enabling the renderer. The presenter now owns active contour visibility: at least two points enable it, clearing it disables it. The scene's idle flag and authored material remain unchanged.

The shipping input path is `EarthInputAdapter` → `EarthActionRouterBehaviour` → `MagicInputController`. The router's serialized null reference is resolved by `MagicInputController.Awake`; it is not evidence of missing routing. Single LMB input retains the existing 80 ms dual-mouse disambiguation buffer. Arena drawing uses the stable surface-query handle and locked face plane. No legacy gesture path or conflicting action map was enabled.

## Validation contract

`PlatformPreviewContractTests` verifies a disabled authored contour becomes visible while drawing and disappears after clear. `PlatformInputProductionTests.LiveCombatMouseContourPreviewsThenCreatesPlatform` starts the actual frontend bot match, waits for combat, pairs a physical Input System test mouse, draws a closed ground contour through the shipping router, and checks both live preview and exactly one successful pooled platform creation. The old load-only physical platform test does not enter combat and therefore does not cover current frontend permission gating.

Public editor entrypoints: `Elemental.Tests.EditMode.PlatformInputQaLauncher.Edit()` and `.Play()`. Reports: `BuildReports/PlatformInputEdit.json`, `BuildReports/PlatformInputPlay.json`, routing telemetry `BuildReports/PlatformInput/routing.txt`. Production screenshots are `contour.png` and `platform.png` in that folder. Execution is owned by the main agent.

## Creation diagnostic

The first real-input fixture drew a 0.2065 m² footprint, below the unchanged 0.8 m² minimum. The second diagnostic proved physical release was consumed (`Cancelled` / owner `None`, adapter and mouse no longer held), rather than lost by the router. Its failure exposed a separate actual feedback defect: `TryRaisePlatformOnSurface` returned false without updating the user-facing status. The controller now reports the footprint area and the direction of correction for undersized/oversized horizontal footprints. Unsupported face/pool cases also report failure rather than leaving the prior drawing status displayed. Area limits and the input routing grammar were not changed.

The corrected production fixture finds a physically visible ground contour whose world-space area lies inside the existing construction limits; all creation and visible-preview assertions remain strict. Edit **2/2 passed** at `2026-09-08T12:54:18.7335493Z` (0.02497 s). Production Play **1/1 passed** at `2026-09-08T12:55:48.6804250Z` (19.4653 s): 12 preview points, 1.618975 m², exactly one successful command and acquired platform; release leaves the router at None and both physical/semantic primary buttons released.

Final visibility polish raises only a too-thin runtime stroke to0.065m and supplies a warm cream tint through a cached MaterialPropertyBlock during Configure/Awake. The shared MagicPreview material and geometry depth offset remain unchanged, and authored wider strokes are preserved. The third Edit contract checks those properties. All three contracts passed within the **26/26** combined Edit run at `2026-09-08T13:07:47.8441798Z`. The final platform production case also passed in the combined Play run at `2026-09-08T13:09:56.4740489Z` (that run had a separate menu-camera timing-fixture failure): 12 points,1.623052m²,one command. Reviewed final `contour.png`: the cream outline is now clearly visible in front of the player; `platform.png` shows the created solid. No overall combined-suite pass is implied by this platform-specific evidence.
