# Animation semantic/ownership audit — 2026-09-04

## Measured state

- `BuildReports/SeptemberAnimationPlay.json` is fresh at `2026-09-04T17:17:01Z`
  and passes 5/5. It supersedes the earlier 1/3 report.
- Its saved per-test samples keep head-to-hips projection above 0.49 m in the
  idle/turn/stop and staged owner cases. The eleven-technique case stays above
  0.52 m. Maximum absolute pitch is 11.96 degrees in idle/turn/stop and 55.92
  degrees across casts, inside the current 65-degree gate.
- `BuildReports/SeptemberSurfacePlay.json` at `2026-09-04T20:12:02Z` remains
  red (0/1). The current failure is `Final ankle is outside the actual terrain
  fixture` at `SeptemberAnimationSurfaceRuntimeTests.cs:212`. Surface-following
  acceptance therefore remains open.

## Static controller result

- The saved `KayKitMage.controller` has the intended upper-body mask: root,
  legs and foot IK are excluded; body, head and arms are included.
- The saved direct tree contains eleven ordered `EarthPose01..11` inputs and its
  GUIDs resolve to the paths declared by `EarthHumanoidMotionSetup`.
- Turn-in-place contains authored left turn, neutral upright idle and mirrored
  right turn. The state has no outgoing Animator transition because
  `HumanoidCharacterPresentation` plus `EarthTransitionDirector` owns both entry
  and exit. The fresh runtime test observes that owner and passes.
- The impact fallback layer is named `Impact Additive` but is currently an
  Override layer. This is internally consistent with the non-additive Mixamo
  recoil source and is normally bypassed while `HumanoidProceduralBodyResponse`
  owns impacts. Do not switch it to Additive without first baking an additive
  reference pose and validating the fallback path.

## Remaining risks

1. The current 11-technique PlayMode gate proves only that the right hand moves.
   It does not prove the intended saved child clip was selected. A duplicated or
   reordered direct child can remain green. The staged asset and runtime tests
   close this gap with exact path and one-hot parameter checks.
2. The current idle/turn/stop test checks final foot error but never samples knee
   angle or one-frame knee change. A visibly bent or snapping leg can remain
   green. The staged walk-stop test measures both knees through real motor input.
3. `EarthChoreographyDirector` writes `EarthEffort`, `EarthBrace`,
   `EarthGrounding`, `EarthPrecision`, `EarthPhase` and `EarthDialect`, but the
   controller only declares those parameters. No BlendTree, transition, state
   speed/time field or other runtime component reads them, and nobody reads
   `EarthChoreographyDirector.CurrentSample`. Technique slot and
   `EarthMotionTime` currently create the visible variation; choreography tuning
   itself is dead presentation data.
4. Every saved `EarthMagicMotionProfile` entry still uses the identical default
   0.52 contact marker and identical phase durations even though the slots use
   distinct source clips. Structural validity is proven; authored hand/VFX
   contact alignment per clip has not been calibrated or captured.
5. Pull Stone and Armor Assemble deliberately share the same two-hand cast clip.
   Several other slots use non-magic placeholders (Wheelbarrow Dump, Lead Jab,
   MMA Kick, Punch Combo, Punching). This is not a routing error, but it means
   “eleven semantic slots” is not yet evidence for eleven final-quality motions.

## Focused acceptance

After copying the staged sources as described in `README.md`, both focused menus
must be green. Then capture each slot at its saved contact marker with the actual
VFX/event visible in the same frame. Accept a timing entry only when the authored
hand/body beat and the gameplay result agree; do not make all clips inherit the
same marker merely because the profile validates.
