# Duel HUD and combat follow-up

2026-09-06, working tree based on main `1235579`. This is a feature implementation;
the earlier outstanding mantle/armor/startup visual gates are not closed by it.

## Saved integration

`EarthDuelHudAuthoring.InstallAll` updates the existing EarthCoreSlice scene in
place, binds the duel, player, planet, main arena, readiness gate and accepted
magic/shot events, then bakes and installs the kick clips. It does not rebuild
terrain or decorative structures. Dual-mouse control is now explicitly serialized
instead of being available only after the runtime router's Configure path.

The production UI uses UI Toolkit and Painter2D geometry. HP/mana fill upward,
the top panel shows team scores and the independent 300-second match clock, and
the larger hologram parallel-transports its view around the planet to keep the
player visible without a pole flip. Arena markers are dimmed on the back side.
Team mapping follows the existing authored actor palette: red player, blue rival;
`playerIsBlue` supports the reversed mapping without changing scoring authority.
The HUD has no scene camera, lighting or post-process ownership. Existing reticle
feedback is retained; detailed legacy panels are an inspector diagnostic option.

Display mana has a floor of 15/100, 0.35s recovery delay, and 25 units/second
recovery. Accepted paired shots cost 4/10/20 for hand/kick/spin; other accepted
commands cost 4/10/20 by operation. Active gravity/vector/armor/surf costs 8/sec.
It is never consulted by action admission. Round-ready and round-over boundaries
stop/restore authored control components; health/death remains a runtime owner.

## Animation source finding

The existing MMA import is a right-foot attack cropped at its maximum extension:
0.833 seconds, measured right-foot lift 0.7644m versus left 0.0313m. Copying that
clip directly would preserve the user's reported extended-leg cutoff. The new
canonical kick maps the contact to 0.5 and retracts through its source poses.
The spin maps contact to 0.6, rotates the Humanoid body while preserving actor
root position, and also returns to its starting pose. Motor owns physical hop.

## Evidence

- `TestResults/DuelAcceptanceEditFinal.xml`: 35/35 passing at 23:27:58 UTC
  September 5 (September 6 locally), covering final health/mana/wall/combo logic,
  canonical clips, independent full-body layer and the original 11 magic slots.
- `TestResults/DuelAcceptancePlay5.xml`: 9/9 passing at 23:24:29 UTC. Saved HUD,
  real damage and once-only scoring, readiness ordering, round restart, wall
  contact/release, physical rapid paired clicks, three combo frame-rate targets,
  and the existing responsive paired-input/no-stale-replay check all pass.
- `Logs/DuelIndependentLayerInstall.log`: successful compile and saved installation.
- `BuildReports/EarthSpinKick/BakeLatest.txt`: sampled spin 360 degrees,
  root drift 0m, right contact lift 0.761m. This is a source-clip sampling gate,
  not production animation or visual acceptance.
- `BuildReports/DuelHud`: inspected 720p/1080p/ultrawide/4K UI captures and
  `gameplay-hud.png` from the actual scene camera with the production HUD. Latest
  60-frame HUD update CPU mean 0.03506ms, p95 0.04240ms, max 0.43380ms. These
  measurements cover `Elemental.DuelHud.Tick`, not total rendering cost.
- `BuildReports/QuickStoneCombo`: contact and mid-spin captures were inspected.
  Runtime hips turns at 30/60/120 FPS targets measure 350.06/360.04/361.18 degrees,
  while the gameplay root remains directed at the aim. These are frame-rate
  settings, not measured sustained hardware FPS. Both kick feet visibly lift;
  active foot IK is released and head clearance remains valid.
- `TestResults/DuelWallVisualFinal.xml`: final 1/1 visual/profiler recapture.
  Inspected `BuildReports/WallBrace/Braced.png` and `Released.png`; hand distances
  are 0.02153/0.02907m. Wall-step marker mean is 21.39 microseconds, max 56.0,
  from 30 samples (not total animation graph cost).

The first runtime checks caught an inherited upper-body mask hiding kick legs
and insufficient reach from the idle guard at a wall. The final controller uses
independent full-body A/B states, while a bounded spine alignment job precedes
the existing arm solver. Neither failure was waived. Hidden batch input also
needed temporary focus-routing settings; the test fixture restores those values.

All changes are uncommitted in the working tree based on main `1235579`.
This acceptance covers the requested HUD, paired-click combo and wall brace.
Earlier moving-ledge mantle, armor coverage and strict startup-cover gates remain
separate; the SONIC experiment remains opt-in, not the production pose owner.

Related contracts: `duel-health-round-contract.md`, `QUICK_STONE_COMBO.md`,
`WALL_BRACE_IMPLEMENTATION.md`.
