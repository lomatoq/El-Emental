# Surf pillar jump patch

This staged patch adds one authoritative `Space` action while `EarthActionRouter`
owns an active surf session:

1. The press edge commits `PillarJump` and resets the surf input owner.
2. `EarthPillarMobility.TryLaunchAtCharge` schedules the existing support query,
   typed `PillarRaised` event, visual pillar and motor launch exactly once.
3. `EarthSurfController.BreakForPillarJump` sends a severe event through the
   existing finite-cell integrity graph, releases all twelve board meshes through
   the existing debris pool and adds a tuned upward scatter velocity.
4. The motor's external-launch window cannot be mistaken for eligible surf
   support, so held Shift+forward cannot recreate a board during ascent.

No input callback is added. `EarthInputAdapter` remains the only device boundary,
the router consumes the press edge, and `PlanetInputReader` never receives an
ordinary jump command for this action.

## Tuning

`EarthSurfProfile` exposes:

- `pillarJumpCharge01` (default `0.24`), which maps to roughly `3.78 m` of the
  existing authored pillar rise envelope;
- `pillarJumpScatterSpeed` (default `3.2 m/s`), applied only to stones released by
  this trick.

Old profile assets with an absent or zero field use those defaults at runtime.

## Integration

Copy the contents of `after/Assets` over the project `Assets` tree without deleting
other files. The patch is based on the current working copies, including unrelated
already-integrated changes that were present on 2026-09-05.

## Focused validation

After Unity compiles with zero new warnings, run these menu items in order:

1. `Elemental/QA/Run Surf Pillar Jump EditMode Tests`
2. `Elemental/QA/Run Surf Pillar Jump PlayMode Test`
3. `Elemental/QA/Run Finite Surf PlayMode Tests` for the surrounding surf regression
4. `Elemental/QA/Capture Surf Pillar Jump Visual Proof` after copying the separate
   `visual/Assets` overlay. This injects physical Shift+W then Space through the
   production `PlayerInput` in `EarthCoreSlice`, disables the rival, and writes a
   side-view three-frame sequence plus `CaptureReport.json` under
   `BuildReports/EnvironmentAnimationRescue/SurfPillarJumpVisualQa`.

The focused EditMode report must show `2/2` passing. The PlayMode test must prove a
single pillar event and command sequence, twelve released stones, inactive board,
positive rider rise and upward velocity, and rejection of an immediate second
launch. Unity validation was intentionally left to the root agent; no Unity command
or Asset refresh was issued while staging.
