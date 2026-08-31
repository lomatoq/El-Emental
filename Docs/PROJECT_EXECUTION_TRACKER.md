# El-Emental project execution tracker

Updated: 2026-08-31

Snapshot branch: `codex/foundation-stability-rescue`

Tested implementation commit: `8c2e6245a0e4fe1e169b63998e918ce800477b36`

Evidence condition: tested 2026-08-31 from the exact implementation content committed above;
later documentation-only commits do not invalidate the code/test snapshot.

Technical context: [`Docs/PROJECT_TECHNICAL_STATE.md`](PROJECT_TECHNICAL_STATE.md)

## Current verdict

**M11 is in foundation stabilization/integration, not acceptance-green at the tested commit.**

The branch contains remote base `e401a22` plus tested local integration commit `8c2e624`. The shared
worktree also contains user-owned modified/untracked evidence and Unity recovery scenes;
preserve them. The generated scene has now been rebuilt after the rig/import changes. A fresh full
EditMode run passes `586/587`; the only failure is the stale Native Windows evidence containing 186
warnings. Fresh targeted PlayMode foundation regressions pass `2/2`. These runs validate the new
rig/support/input seams, not the entire M11 focused or broad PlayMode acceptance chain.

Do not start another ability, progression layer, or content expansion until the generated scene
is rebuilt and the M11 acceptance chain is fresh and green.

## Evidence snapshot

| Evidence | Timestamp (UTC) | Result | Interpretation |
|---|---:|---|---|
| `BuildReports/FoundationWorkingTreeEdit-20260831.xml` | 2026-08-31 14:45 | `586/587` passed | Fresh full EditMode working-tree run. Only the zero-warning build-evidence test fails on the existing 186-warning report. |
| `BuildReports/FoundationWorkingTreePlay-20260831.xml` | 2026-08-31 14:48 | `2/2` passed | Fresh targeted scene-secondary-motion and dynamic-debris support regressions after generator rebuild. |
| `BuildReports/Mvp01FocusedEdit.json` | 2026-08-31 09:48 | `274/274` passed | Strong focused pure/editor coverage, but predates HEAD (`11:13 UTC`). Stale for current commit. |
| `BuildReports/Mvp01FocusedPlay.json` | 2026-08-31 09:28 | `8/18` passed, `10` failed | Current working-tree integration evidence is red. See failure groups below. |
| `BuildReports/Mvp01RescueCurrent.json` | 2026-08-31 09:28 | `success: false` | The latest aggregate acceptance marker is red. |
| `BuildReports/BrokenCrownPlay.json` | 2026-08-31 09:49 | `1/1` passed | Later targeted arena contract pass; not a substitute for the focused suite. |
| `BuildReports/SurfFinitePlay.json` | 2026-08-31 06:35 | `3/3` passed | Finite surf contract is locally green on its captured snapshot. |
| `BuildReports/AnimationContactAcceptanceEdit.json` | 2026-08-31 06:22 | `7/7` passed | Pure animation-contact gate is locally green. |
| `BuildReports/AnimationContactMatrixPlay.json` | 2026-08-31 06:21 | `1/1` passed | 30/60/120 matrix artifact passes on its captured snapshot. |
| `BuildReports/CharacterAnimationVisualAuditPlay.json` | 2026-08-31 06:31 | `1/1` passed | Targeted animation audit passes; ADR 0033 still requires the complete same-worktree gate. |
| `BuildReports/AcceptedMvpEvidencePlay.json` | 2026-08-31 05:07 | `1/1` passed | Older than the later red aggregate marker; historical only. |
| `BuildReports/Mvp01Profiler.json` | 2026-08-31 05:06 | standalone 720-frame pass | CPU/total p95 `8.335 ms`, zero measured steady-state GC; GPU unavailable and waived; predates HEAD. |
| `BuildReports/Mvp01ProfilerEditorDiagnosticLatest.json` | 2026-08-31 09:28 | failed | Editor diagnostic p95 `19.1631 ms`; foot-contact and render audit fail. Not authoritative, not green. |
| `BuildReports/NativeWindows.json` | 2026-08-31 05:05 | build succeeded, 186 warnings | Fails the repository zero-warning acceptance rule. |

All timestamps above are file contents, not filesystem modification times. Historical rows below
the two `FoundationWorkingTree` artifacts predate remote base `e401a22` (created at
`2026-08-31T11:13:40Z`); they remain reproduction history, not validation of `8c2e624`.

## Now / Next / Later

### Now — restore one trustworthy M11 golden path

1. Preserve the shared dirty worktree; identify whether any active Unity/test process owns the
   latest reports before starting a new run.
2. Rebuild `EarthCoreSlice` through `M3EarthCoreSetup.Configure()` after any further source asset,
   importer, profile, or generator change. The current rig/import rebuild is complete.
3. Run the complete focused PlayMode chain from the tested implementation. Full EditMode and the two
   new foundation PlayMode regressions are fresh; the broader PlayMode scope is not.
4. Repair remaining failures one authority seam at a time in this order: physical input/targeting;
   support/landing; visible Humanoid/ragdoll reset; projectile contact; accepted evidence runner.
5. Run the accepted-evidence scenario, standalone 720-frame profiler/capture, and a warning-free
   Native Windows build. Record commit, machine, resolution, mode, sample count, and whether GPU
   timing was actually available.
6. Accept or keep proposed ADR 0033 based on its complete same-worktree verification gate, then
   reconcile M11's status/radius/performance prose with the evidence.

### Next — close the vertical-slice gate

- Run repository-wide EditMode and PlayMode after focused M11 is green; M11 explicitly says the
  full suites were not rerun in its earlier focused rebuild.
- Recheck the golden path manually from fresh launch through movement, physical input, matter
  extraction/launch, structure fracture/repair, rival impact, both KO paths, respawn, and replay.
- Capture representative 1920x1080 frames and normal-speed video from the shipping camera; compare
  silhouettes, contact, foot motion, shadows, arena seating, and both fighters.
- Measure current-head CPU, GPU, memory, GC, frame pacing, loading, and queue peaks on the reference
  PC. Add a minimum-target machine before claiming production performance.
- Protect the new `CharacterSupportAuthority` -> `PlanetMotor` boundary with focused support,
  landing, moving-platform, generation-switch, and seam/debris coverage.
- Update the current milestone and `.solo-studio` summaries to link these two canonical living
  docs instead of carrying divergent status claims.

### Later — explicitly outside the M11 rescue

- Full-game atomic save/backup/recovery and durable replay files.
- Production network transport, real multi-machine sessions, disconnect/reconnect, service failure,
  relay/NAT, security, and platform integration.
- Controller/accessibility matrix, onboarding with fresh players, localization, store/release,
  telemetry/privacy, support, and rollback operations.
- HP/score/rounds/victory UI, progression, NavMesh/behavior tree, or another enemy/element only
  after the Earth vertical slice is acceptance-green and externally playtested.

## Completed work with evidence

“Completed” here means the code/asset/decision exists at HEAD. It does not imply the current HEAD
passes the full acceptance gate unless the evidence column says so.

| Work | State at HEAD | Evidence / commits | Remaining caveat |
|---|---|---|---|
| MVP 0.1 rescue baseline | Implemented and previously focused-green | `08afcd3` “Complete MVP 0.1 earth core rescue”; `add1e3a` evidence update; ADRs 0029/0030 | Later integration changed the scene and current focused PlayMode is red. |
| Broken Crown import/fracture/runtime integration | Implemented | `c5ebeab`; ADR 0031; `BrokenCrownPlay.json` `1/1` | Full focused suite and fresh current-HEAD visual/interaction matrix still required. |
| Animation/contact/rendering rehabilitation | Implemented under proposed decision | `c5ebeab`; ADR 0033; targeted animation reports | ADR 0033 remains proposed; latest bot telemetry has `hardGatesPassed: false`. |
| Finite surf semantic graph | Implemented and targeted-green | ADR 0030 follow-up; `SurfFinitePlay.json` `3/3` | Protect in full focused suite; no broad acceptance implied. |
| Shared Earth matter mass policy | Pure policy + tests + arena/decor adapters present | `5054d09`, `930b8bd`, `b7d75d3`, `3a33a11`, `3d87414`, `6da1ae9`; `EarthMatterMassPolicyTests.cs` | Current-HEAD runtime/profile evidence is missing; integration is not universal by file search. |
| Gameplay-locked celestial clock/key | Policy, tests, and presentation wiring present | `5685799`, `e1f7e61`, `8a4dbc4`; `CelestialLightingAuthorityTests.cs`; `CelestialSystemBehaviour.cs` | Fresh rebuilt-scene visual/render audit required. |
| Classified character support policy | Integrated at `8c2e624` through `CharacterSupportRuntimeAdapter` and `PlanetMotor` | `CharacterSupportAuthorityTests.cs`; `FoundationWorkingTreePlay-20260831.xml` support regression | Complete focused landing/moving-platform PlayMode remains pending. |
| Linebreaker rig/secondary-motion rescue | Weighted source and runtime FBX integrated; generated scene rebuilt | `LinebreakerRigged_weighted.blend`; four weight/rig reports; `FoundationWorkingTreePlay-20260831.xml` | Full visual gait/ragdoll/KO gate and capture remain pending. |
| Canonical input boundary cleanup | Rumble lookdev/VFX shortcuts routed through `EarthInputAdapter` | `FoundationWorkingTreeEdit-20260831.xml`; full source-scan test passes | Physical mouse golden-path failures from the older aggregate still require full focused rerun. |
| Duel-space shadow stabilization | URP/project settings changed | `c606e2a`, `e401a22` | No report or capture after either commit. |

## Latest focused PlayMode failures to reproduce

Source: `BuildReports/Mvp01FocusedPlay.json`, 2026-08-31 09:28 UTC. These are observations from
that run, not assumptions about current HEAD.

1. Broken Crown semantic placement reported floor/rock/rubble gaps and penetrations.
2. A physically held decor rock acquired but received zero control force.
3. Near/far physical mouse wall strokes committed no command and ended cancelled.
4. Stationary physical LMB never started terrain extraction.
5. A high fall onto the landing cushion did not satisfy the safe-landing expectation.
6. Player/bot visible ragdoll atomic reset expectation failed.
7. Rival did not share the player's Humanoid Avatar/controller in the loaded scene.
8. Stomp stone launch was `0.2919` where the test expected greater than `20`.
9. Accepted evidence aggregate wrote `success: false`.
10. A shallow near-surface projectile graze entered `Sleeping` instead of staying `Armed`.

The later `BrokenCrownPlay.json` pass may supersede item 1 only in its narrow scenario. Rerun the
complete focused suite before deleting, reclassifying, or fixing any item.

## Corner-case and regression matrix

| Area | Protected corner case | Primary seam / evidence |
|---|---|---|
| Input ownership | 80 ms dual-button decision preserves the original full primary path; quick drag is draw, stationary hold is acquire/extract, chord is not order-dependent | `EarthActionRouterTests.cs`, `EarthCoreVisualRuntimeTests.cs`, `EarthContextualInputRuntimeTests.cs` |
| Physical targeting | Dense arena dressing cannot hide the canonical planet; caster cannot target itself; viewport-normalized intent survives camera change | `EarthInputAdapter`, `EarthTargetQueryService`, physical-input focused PlayMode tests |
| Terrain transaction | Failed/cancelled extraction exposes neither hole nor fragment; commit exposes exactly one reserved fragment; buffered release survives commit latency | `TerrainExtractionTransactionTests.cs`, `EarthCoreReplayTests.cs`, `EarthPlayerGoldenPathRuntimeTests.cs` |
| Surface/support | Nearest valid constructed support wins; generation changes invalidate stale handles; seam/debris cannot steal stable character authority; exact-contact fallback prevents one-frame airborne state | `EarthSurfaceContractTests.cs`, `EarthSurfaceQueryRuntimeTests.cs`, `CharacterSupportAuthorityTests.cs`, `PlanetMotorPlayModeTests.cs` |
| Platform fracture | Cast creates no runtime objects/collider burst; early impact queues until ready; one cell prepares per frame; settled support velocity is zero | ADR 0029, `EarthPlatformPreparationBudgetTests.cs`, `EarthPlayerGoldenPathRuntimeTests.cs` |
| Arena fracture | Ordinary hits cannot destroy meteor floor; vector/pluck releases one cell; gravity disassembly is progressive; complete repair restores intact proxy | ADR 0031, `BrokenCrownArenaRuntimeTests.cs` |
| Matter identity/mass | Stable provenance survives representation change; return is atomic; authored/collider masses resolve to one gameplay policy without zero/NaN or scale drift | `EarthMatterKernelTests.cs`, `EarthReturnSessionTests.cs`, `EarthMatterMassPolicyTests.cs` |
| Projectile contact | Shallow tangent graze stays armed; direct wall/character hit spends once; sweep/callback cannot double-apply impact | `EarthProjectileSurfaceContactSolverTests.cs`, `EarthProjectileSurfaceContactRuntimeTests.cs` |
| Character outcome | One large stone is recoverable knockdown, not KO; three distinct clustered sources can KO; landing cushion suppresses fall KO; impact applies once | `CharacterOutcomeResolverTests.cs`, `EarthCharacterFeelTests.cs`, `EarthMvpEncounterRuntimeTests.cs` |
| Animator/ragdoll | Animator and PhysX never own the same bones; both fighters use visible 11-body rigs; KO/disable/cancel resets control, colliders, bones, IK, and materials atomically | ADR 0029/0030, `ActiveRagdollRuntimeTests.cs`, `EarthMvpEncounterRuntimeTests.cs` |
| Foot contact | No simultaneous locomotion locks; swing must re-arm; support generation change does not retain stale anchor; IK/pelvis/knee steps stay bounded at 30/60/120 | ADR 0033, animation contact/matrix/visual reports and telemetry |
| Surf | Time/coplanar transfer causes no loss; wall damage stays in lower 32%; one contact latches once; small body/character is not a board-killing wall | ADR 0030 finite-surf follow-up, `EarthSurfIntegritySolverTests.cs`, `SurfFinitePlay.json` |
| Camera/rendering | Both fighters remain in sharp envelope; unsafe motion follows accepted DOF policy; one sun/ambient owner; shadow/SSAO changes do not hide contact or affect gameplay | ADR 0032/0033, `EarthCinematicDepthOfFieldSolverTests.cs`, render audit/captures |
| Scene rebuild | Rebuild preserves 55.1 m world, approved arena root/rival spawn, exact floor collider, no missing scripts, stable imported child transforms | `M3EarthCoreSetup.cs`, `BrokenCrownArenaImporterTests.cs`, `BrokenCrownArenaRuntimeTests.cs` |
| Save/replay | Voxel v1 reads into v2; invalid version/flags/edit count reject; commands stay tick-ordered; replay reproduces ordered edits | `VoxelPlanetStateTests.cs`, `ReplayAuditTests.cs`, `EarthCoreReplayTests.cs` |
| Capability | NativeHigh/NativeLow/Web reduce visuals and budgets without changing canonical gameplay; unsupported features reject or use documented fallback | `CapabilityProfileTests.cs`, `CapabilityRuntimeTests.cs`, `Docs/performance-budgets.md` |

## Blockers, stoppers, and active risks

| ID | Severity | Evidence / leading indicator | Mitigation | Fallback | Decision date |
|---|---|---|---|---|---|
| B-001 Tested implementation is only partially validated | Stopper | Full EditMode `586/587` and targeted foundation PlayMode `2/2`; complete focused/broad PlayMode not rerun | Run focused chain and accepted scenario from `8c2e624` (plus docs-only changes) | Use old reports only to prioritize reproduction, never as current verdict | Next integration session |
| B-002 Focused PlayMode red | Stopper | `Mvp01FocusedPlay.json`: 10/18 failed | Reproduce, group by authority seam, fix/rerun one seam at a time | Cut non-core presentation only if it preserves input, terrain/matter, support, and duel truth | Before M11 acceptance |
| B-003 Warning-free build absent | Stopper | `NativeWindows.json`: succeeded with 186 warnings | Classify project vs external warnings and produce a fresh zero-warning build | No release/acceptance claim; warnings may be waived only by an explicit documented owner/removal milestone | Before M11 acceptance |
| B-004 ADR 0033 still proposed | High | Status says implementation under live verification; latest bot telemetry `hardGatesPassed: false` | Complete the exact same-worktree telemetry, seating, visual, and profiler gate | Restore locomotion contact weight to zero while retaining telemetry and authored motion, per ADR rollback | Before accepting ADR 0033 |
| R-001 Generated integration surface | High | 3.5k-line setup, 175k-line generated scene, frequent broad rebuilds | Keep source-of-truth changes in profiles/importers/setup; validate after every rebuild | Revert one generator concern through its ADR seam; never hand-maintain divergent scene edits | Ongoing |
| R-003 Evidence freshness/overwrite | High | `Latest`/`Accepted` files conflict; tracked baseline differs from dirty working result | Include commit/branch in reports or companion manifest; never infer pass from filename | Preserve timestamped raw artifacts and cite immutable ones | Next evidence-tool change |
| R-004 Input-camera-arena coupling | High | Three physical input failures after Broken Crown integration | Keep device boundary singular; test actual `<Mouse>` path at near/far/collider-dense views | Reduce non-authoritative ray blockers/camera composition without bypassing canonical surface query | Before M11 acceptance |
| R-005 Animation/physics handoff | High | Ragdoll reset/shared controller failure; bot telemetry hard gate false | Instrument owner/phase and reset; run interrupts, KO, respawn, arbitrary gait phase | Authored locomotion + foot IK zero; retain physical KO only if atomic reset passes | Before M11 acceptance |
| R-006 Performance claim gap | High | Standalone GPU unavailable/waived; editor diagnostic red; newer code unprofiled | Fresh standalone target-device capture with CPU/GPU/GC/frame pacing and render audit | Disable degradable atmosphere/DOF/motes before changing gameplay fidelity | Before vertical-slice acceptance |
| R-007 Package/maintenance surface | Medium | Git MiniBokeh, prerelease AI Assistant, AI Inference, VFX Graph present | Confirm usage, pinning, license, platform/build-size impact, and removal seam | Remove unused package; keep project-local/URP fallback and CPU gameplay truth | Before dependency freeze |
| R-008 Save/network/release gaps | Medium now, release blocker later | Codec/harness exist but no runtime disk save or production transport | Keep outside M11; create separate gated milestones with recovery/failure tests | Ship local sandbox only if product scope explicitly accepts it | Before Alpha/online commitment |

## Pending M11 acceptance gates

| Gate | Required proof at one commit/worktree | Current status |
|---|---|---|
| Compile/import | Zero compiler/shader warnings/errors, no missing scripts after rebuild | Editor refresh reports zero compile errors; warning-free build remains red/unknown because the last Windows report has 186 warnings |
| Focused EditMode | All selected M11 tests green | Fresh full EditMode `586/587`; only external build-evidence warning gate fails |
| Focused PlayMode | Physical input, terrain/matter, platform, support, duel, projectile, arena, surf all green | New foundation subset `2/2`; complete focused suite not rerun, older aggregate remains historical red evidence |
| Accepted scenario | `Mvp01RescueCurrent.json` success and scenario test green | **Fail:** latest aggregate `success: false` |
| ADR 0033 | Contact telemetry, terrain corpus, seating, 1920x1080 visual, representative profiler green together | **Pending/proposed** |
| Performance | 720 representative standalone frames, zero-GC gate, CPU/GPU budgets or explicit justified unavailable metric, queue peaks | Historical CPU pass; no current-HEAD proof; GPU waived |
| Visual | Fresh shipping-camera frames/video show both fighters, seating/contact, fracture, shadows, no missing main capture | **Unknown at HEAD**; working tree lacks `Mvp01RescueCurrent.png` |
| Native build | Reproducible Windows build with zero project warnings/errors and smoke pass | Build succeeds but warning gate fails |
| Broad regression | Full repository EditMode + PlayMode after focused acceptance | Full EditMode rerun (`586/587`); full PlayMode not rerun |
| Player evidence | Fresh player can understand and repeat the duel/earth loop without explanation | No external playtest evidence recorded |

## Handoff checklist for every agent/chat

- [ ] Read `PROJECT_TECHNICAL_STATE.md`, this tracker, `Docs/architecture.md`, current M11, and
      only the ADRs relevant to the change.
- [ ] Record `git status --short --branch`, branch, and full HEAD. Preserve all user-owned dirty,
      untracked, recovery, report, and Unity-generated files.
- [ ] Name the one authority seam and player-visible outcome being changed. Do not expand scope
      while a stopper above is red.
- [ ] If profiles, imports, setup, or generated scene contracts changed, rebuild through
      `M3EarthCoreSetup.Configure()`; do not hand-edit generated `EarthCoreSlice` as source.
- [ ] Run the narrow pure test, the corresponding runtime/physical-input test, then the focused
      suite. Run broad suites/build/profile when the acceptance gate requires them.
- [ ] Record exact totals, failure names, UTC, machine/mode/resolution, report paths, and commit.
      Label missing GPU/player/target-device evidence as unknown.
- [ ] Check `git diff --check`; verify only intended files changed; do not commit generated evidence
      that contradicts the claimed status.
- [ ] Update the affected rows in both living docs when a public contract, accepted result,
      blocker, fallback, or current milestone materially changes.

## Small append/update protocol

Keep this tracker short enough to reread at every handoff:

1. Replace the header snapshot only after inspecting the exact checkout.
2. Update `Current verdict`, `Evidence snapshot`, and affected gate/risk rows; do not rewrite
   history to make a decision look cleaner.
3. Add completed work only with a path, test/report, ADR, or commit hash. Say “implemented” when
   tests are stale; say “accepted” only when the required gate is green at the same commit.
4. Move resolved incidents out of active blockers after recording the closing evidence in the
   milestone/ADR or an immutable timestamped report. Keep one short completed row here.
5. Put deep rationale in an ADR, detailed evidence in `BuildReports`/milestone QA docs, and stable
   architecture in `Docs/architecture.md`; link it here rather than duplicating it.
6. Never use a `Latest`, `Current`, or `Accepted` filename as status by itself. Read its result,
   timestamp, capture conditions, and commit provenance.
