# Held dual-mouse pillar row

Inspection baseline: main `56e7d9e1`. User reports that holding LMB and RMB and drawing a line no longer creates the row of pillars.

The shipping path is `DualMouseEarthGestureSolver` → `EarthActionRouterBehaviour` → `EarthDualMouseAbilityController.CastPillarCrest` → `EarthPillarWavePool.LaunchCrest`. Ordinary LMB wall/platform drawing stays owned by MagicInputController. The dual solver retains its 80 ms initial chord arbitration, accepts any drawing direction, and commits after both buttons are released.

The original admission gate rejected a row whenever any existing column was still anchored or the previous complete wave's protection window was active, even with enough unused entries. LaunchCrest now reserves only genuinely free capacity; Acquire still never recycles active geometry. The ordinary full-wave exclusivity gate is unchanged.

The initial production diagnostic passed 1/1 at 13:46:08 UTC: tracked=true, free columns 96→91, zero rejections and zero ordinary LMB commands. This confirms fresh paired input works; the fix targets repeat admission while geometry remains visible.

`PillarRowInputProductionTests.RealPairedMouseDrawCreatesPillarRowAndDoesNotBecomeSingleMouseMagic` now repeats the physical mouse stroke while the first row is anchored, reverses button-release order, and checks fresh allocation without changing original column generations or leaking ordinary single-button magic. It captures `BuildReports/PillarRowInput/row.png` and routing telemetry. Two Edit cases enforce both-button release and exactly one crest commit. Execution evidence is recorded below.

The landing-slam integration adds `LaunchLandingPulse(point, up, forward, power, caster)`: three rings of 6/12/18 pooled cells emerge outwards across the full circle, with 85 ms ring delays. Capacity is reserved atomically for all 36 cells; the method does not replace visible geometry or full-wave topology/timing globals. The production fixture also verifies pulse-over-row, row-over-pulse, and capacity rejection preserving all active generations. Existing meshes, profile assets, and input bindings remain unchanged.

## Verification on September 8

Baseline main 56e7d9e1; changes validated in the working tree. Real paired mouse regression passed in LandingRowAudioPlay (14:11:12Z): first row used5 cells, repeated reverse-release row used5 more while all5 previous cells remained anchored; zero rejected casts and zero leaked ordinary single-button commands. Pulse-over-row, row-over-pulse and full-capacity preservation also passed. Router/dual-mouse contracts included in LandingRowEdit45/45 (14:16:48Z).

The complete real paired-mouse repeat/capacity regression passed again in EarthInteractionFinalPlay at15:14:20Z. Final input/seam contract suite95/95 at15:19:17Z.
