# Quiet clustered ground dust — frozen staging

Seven C# files in `after`. Four current-source before snapshots, SHA manifest and source.patch; all before hashes matched at freeze. No actual Assets, Unity, scene, fog or material changes performed by this lane.

## Integration

1. After the editor is responsive and tests stop, verify `import-manifest.json`, import `after`, refresh once and check compilation.
2. Open saved EarthCoreSlice, stopped Play. Run **Elemental/VFX/Install Cluster Ground Wisps**.
3. Installer requires exactly one existing EarthSurfaceWindDust emitter and its existing arena/planet/material, plus real FrontendFlowController. It creates no duplicate particle system. On first use it clones the current surface profile to `Assets/Elemental/Content/Profiles/EarthClusterGroundDustProfile.asset`; repeated installation preserves that clone's tuned values. The original profile/material remain untouched. Only the existing dust component is rebound/saved.
4. Roll back by binding original EarthSurfaceWindDustProfile via existing **Install Surface Wind Dust (Preserve Scene)**. Runtime reconfiguration restores the renderer's prior depth-fade property block.

## Behavior and limits

The original native surface-dust system is reused. A0.8s nonalloc physics scan builds at most96 unique settled rock owners from the existing256-collider query. Multi-collider parts of one owner do not create fake density. Local base-height neighbors within3.5m get higher selection weight; overhead neighbors >1.5m away vertically do not count. Up to96×95 simple pair comparisons per scan, no additional physics queries for density.

The optional profile starts with open ground4 particles/s (previous12), stone base24/s, isolated weight0.2 and dense multiplier1.6. A lone stone produces about4.8 local particles/s; a dense scanned neighborhood can reach38.4 local/s plus4 open-ground/s. Per-stone weighted selection further favors clusters.65% of eligible nearest-neighbor selections try the real gap between collider bounds, followed by the existing ground ray; touching bounds use ordinary leeward wakes. Moving debris is excluded until settled. Density counters/gap emissions are exposed for actual QA.

Wisps use opacity0.18, size0.45–1.1m with fine-biased sampling, lifetime2.8–4.2s,0.12m center height and0.6m/s drift. The same soft dust material is retained. Only this ambient renderer receives a short0.025–0.5m soft-depth fade so the low layer is not erased by the broad-fracture fade; the original renderer block is restored on rollback. No fullscreen haze and no collision/gameplay/mass/network writes. Impact bursts remain on the separate unchanged presenter.

Hard limits remain maximum192 native particles, at most4 emissions and4 refresh probes per frame, existing spawn ground probes, no particle collision/trails/lights. Mean dense rate×mean life predicts roughly148 live particles before missed supports/occlusion; this is arithmetic, **not measured runtime performance or guaranteed visual density**. Existing disabled/pause time behavior remains. Reduced Motion uses30% ambient emission,35% speed and no gust oscillation; preferences are explicitly bound and not persisted/changed by the runtime effect.

## Verification

Offline Presentation/Authoring.Editor/Tests.EditMode/Tests.PlayMode compile exit0. The independent arithmetic oracle selects a three-rock cluster900 times versus an isolated rock100 times over1000 evenly spaced samples. No Unity tests or actual rendered performance have been claimed.

Run **Elemental/QA/Surface Wind Dust Edit** (existing tangent policy plus3 new density tests), then **Elemental/QA/Surface Wind Dust Visual Play**. The production fixture now records neighbor/gap/rate counters, checks the native capacity and Reduced Motion rate, restores temporary preferences, and captures the actual saved scene. It still performs existing wall/occlusion and native drift checks.

Review before accepting: quiet open ground versus visibly more frequent wisps between nearby stones; clear silhouettes/feet and floor detail through dust; no persistent opaque orange sheet; same ground height and material color under day/night; separate stronger real impact bursts. Compare on/off using actual fixed camera frames, then run the existing real4cm stone-drop proof with current fog. Record marker time/native count and distinguish CPU marker from GPU performance. If the new geometry has no suitable gaps, the recorded GapEmitted may be0; inspect the scene before treating that as a code defect or raising density globally.
