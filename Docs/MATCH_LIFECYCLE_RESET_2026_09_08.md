# Whole-match arena, clock and menu lifecycle repair

Baseline: main `2f0fbe86`, September 8. Independent runtime audit requested while coordinator owns frontend/audio and Unity execution.

## Evidence and cause

`FrontendFlowController.ShowMain` previously resumed the local pause time scale and only called `SetRoundReady(false)`. That blocks admitted combat and registered input behaviours, but not PhysX, loose stones, gravity or the celestial clock. The restored dynamic authored stones could immediately collide again behind Main, and the daylight clock retained its accumulated night phase. The arena snapshot itself already resets scene-owned authored structure/terrain/matter and correctly leaves unrelated scenes alone.

The existing restore transaction advanced only from `EarthMvpDuelController.FixedUpdate`. Freezing the local world in Main without changing that poll would deadlock the pending restore. `VoxelPlanetBehaviour` drains render/collider queues and confirms edit receipts in `Update`, so the transaction can finish with scaled time frozen.

## Changes

Restore polling now runs in Update, while FixedUpdate refuses match simulation during the pending transaction. Existing `ArenaRestoreCompleted` remains the snapshot-applied event used by online publication. New `ArenaRestoreFinished` fires once only after rebuilt geometry and optional external readiness have completed, deferred respawns are restored, and combat readiness is reinstated.

`CelestialSystemBehaviour.BindMatchLifecycle` explicitly subscribes to whole-match restart and snapshot restore. `ResetForMatchBoundary` resets only runtime elapsed time and immediately evaluates the authored start composition (saved profile phase0.21, daylight). Shared profile values remain untouched. Ordinary life loss/respawn raises neither reset event.

Coordinator integration: bind the scene-owned celestial instance(s) from frontend startup; reset them on ShowMain even if no dirty arena remains; own local simulation pause while Main/settings/countdown or arena restore is pending, restoring the previous time scale for Combat and online transitions. No hidden scene-global locator or Runtime-to-Presentation dependency is introduced.

## Focused verification

Focused Edit **15/15** passed at `2026-09-08T12:54:41.3081347Z`; production Play **3/3** passed at `2026-09-08T12:57:58.1581886Z` in43.4635s. Combined integration is accepted below. `ProductionArenaRestoreTestLauncher.Play()` now selects three tests. Existing real two-cycle Main/new-game test additionally changes the sky to night and verifies exact authored day reset plus20rendered frames of unchanged stone position/day while Main has timeScale0. Existing ordinary-life persistence, canonical family/foreign-world, real grip, armor and terrain hash checks are preserved. New `PausedRematchRebuildsTerrainAndStructuresBeforeCombatResumes` damages a real column and voxel terrain, starts a rematch with FixedUpdate frozen, and requires completed geometry, exact baseline hash, intact column, reset day/health/score and restored combat readiness. Wall duration is emitted in `BuildReports/ProductionArenaRestore/paused-rematch.txt`; this is not a full-game frame budget claim.

`ProductionArenaRestoreTestLauncher.Edit()` includes the existing pure arena-baseline policy and match-state tests plus two readiness-preservation cases: restart clears score/health/time without bypassing an explicitly closed world gate; damage and timer remain stopped until ready. Restore CPU is sampled with the named `Elemental.Duel.ArenaRestore` marker and emitted beside wall duration.

Independent callback review found a second real defect: automatic match-end restore closes `Match.IsReady`; the HUD previously called only `RestartRound`, which preserved that closed gate and produced a fresh-looking but non-combat rematch. The actual named HUD button now follows restart with `SetRoundReady(true)`; that method already defers readiness until any pending restoration completes. Menu BeginBot continues to keep readiness closed through its countdown. The production fixture expires the clock through the real runtime transition and submits the actual `restart-round` button, then requires combat/time to resume and authored daytime to reset.

The first paused-rematch recorder captured zero nonzero CPU samples, so its zero marker values are unavailable measurements, not a zero-cost claim. The fixture completed restoration and actual result-button restart, returned day0.2103, and observed two completion events across the two boundaries.

A repeat-closed control gate now re-disables controls independently re-enabled by respawn/ragdoll adapters, preserving the original enabled-state ledger. A dedicated regression verifies the closed restart and subsequent restoration of originally enabled controls only.

Final combined Play13:09:56 UTC includes passing3/3 lifecycle scenarios and the new closed-control-gate regression. Its sole failure was the unrelated camera fixture's0.8s wait against the authored0.85s transition, subsequently corrected and accepted separately. Main/Pause camera ownership now uses an independent presentation clock, so frozen world time does not stop camera transitions. Aggregate pure/adapter Edit29/29 passed13:15:16 UTC. No full new build or online pair claimed.
