# El-Emental project execution tracker

Updated: 2026-09-05

**Publication checkpoint requested by the user, 2026-09-05:** this snapshot is
intended for `main` and includes the existing local work, source art, assets,
reproducible tools and evidence. It is not an assertion that all checks pass.
Airborne acquisition now physically starts (15:48 Play), but moving-lip hand
contact still fails at 0.925 m versus the 0.35 m gate. Armor coverage still fails
its unchanged distinct-collar-plate gate (3 versus 4); further geometry review is
open. The final native build/strict loading-cover recapture has not yet run.
Camera, outer-ring destruction, charged surf and isolated SONIC acceptances above
remain valid. Large startup cache assets and profiler/cubemap data use Git LFS;
SONIC weights remain reproducibly downloadable under the existing ignore rule.


**Latest verified follow-up, 2026-09-05 15:23 UTC (supersedes older pending notes):**
Gameplay camera fixed in the permanent Cinemachine owner and saved scene: perspective
lens ownership removes physical-gate crop; pitch-distance lift is included in the
arm-height solve. Framing math 7/7 and real neutral/magic/return Play proof 1/1 pass.
Root reviewed the neutral image; both feet have >23% lower-frame margin in this run.
Evidence: `BuildReports/GameplayCameraFraming/20260905T151846308Z`.

SONIC isolated CPU production-actor preview passes at 15:23:30: 252 rendered boxing
samples / 2.554 s, four rolling plans, bilateral hand motion and zero root drift,
with final foot owner retained and camera/UI/bridge/rival state restored. Walk and
boxing PNGs visually inspected. No SONIC component is enabled in saved gameplay.

New user issues under final acceptance: shared interior palette on the heavily cut
sixth outer arch, restored dense fracture cloud, airborne moving-platform catch,
and denser live-bone neck/shoulder/torso armor coverage. These are not accepted
solely from source compilation; focused physical and visual checks are running.


**Latest user corrections, 13:31 UTC:** equipped armor now retains ordinary
locomotion with a separate continuous speed multiplier (~83% compact, ~75%
expanded), without cast stance or disabling automatic mantle. Short Space no
longer selects a premature PillarJump upper-body pose; centered aim removes the
late side bias. New policy **5/5 Edit**, physical input **1/1 Play**, visual proof
**1/1 / four frames**, including 19 rendered jump frames with zero magic layer.
The existing semantic magic regression was rerun: **8/8 at 13:20 UTC**.

Sky final **9/9 captures at 13:31**, envelope math **3/3**: no shell chord or
direction-noise pole, system planet hidden inside the 63.1 m outer atmosphere,
Moon phase/detail and warm horizon color visually reviewed. See DAY_NIGHT_RESCUE.
The initial Surf+Space physical input proof passed (one pillar, 12 stones), but
the user subsequently requested hold-to-charge/release and a physically tilted
column for a forward long jump; that refinement is in progress and supersedes
the instant-launch acceptance. Fresh Player build/cache validation is running.
SONIC anatomical collapse is fixed by retaining Humanoid hips ownership, but
the experimental boxing playback cadence and final framing are still being
reviewed; it remains absent from saved gameplay scenes.

**Current verification, September 5, 11:30 UTC (dirty `d2174ed`):** semantic
magic passes **21/21 EditMode and 8/8 PlayMode**. Real dual-mouse quick stones pass
**30/60/120 Hz, 3/3**. Angular-velocity inertialization passes **6/6**, including
three-axis spatial derivative continuity; the old 176-degree held-arm failure is
fixed and actual held/gravity/vector now passes. Body-relative multi-object carry
passes **1/1**, responsive hand math **7/7**. A real ground-wave commit now emits
its missing presentation event and passes **1/1**. There are eleven shared pose
slots, not eleven total gameplay abilities.

All-eleven current visual matrix **36/36 frames captured at 11:23**, with readable
anticipation/contact/recovery and repeated quick-punch buffers. Head pitch spans
**-21.46 to +28.00 degrees**, valid neck length >=0.1324 m. The excessive source
head tilt is bounded in the existing final body owner; pure head checks **10/10**.
Root reviewed platform, armor and repeated punch contacts. This is scoped visual
and continuity evidence, not a promise of perfect animation in every combination.
The final protected-pose/paused-pose/sonar regression and full movement visual
matrix are still being checked.

Windows Development from saved scenes **builds successfully, zero errors** at
11:26:40 (186 warnings, mainly inference compute variants). Fresh process reports
world ready at **5.589 s** and cover at **2.848 s**; both screenshots were black in
the hidden run, so those files do not establish visual loading acceptance. The
Player also exposes **26 fracture-cache misses**, under investigation. Editor cache
comparison remains **12.75 s cached / 43.74 s uncached**; post-cleanup scene is 6.07 MB.

SONIC remains isolated in `Assets/Experimental/SonicPrototype`, absent from the
saved scene. Unity CPU inference p50/p95 is **79.4/82.3 ms walk**, **75.9/79.3 ms
boxing**. Full Unity/ORT output parity passes (max error ~2.2e-5), G1 math **4/4**.
Humanoid preview is being rechecked after preserving the production PlayableGraph;
inference alone is not visual integration acceptance.

**Environment visual follow-up, 11:00 UTC:** lit dust passes **2/2** focused GPU
Play with day/night delta **0.39619595**, night visibility **0.1160493** and
neutral-reference footprint error **0.001231**. The exact same production particle
layout was captured and visually inspected at Day/Dusk/Night; night is dimmer.
Seismic vision passes **1/1** production lifecycle and **5/5** temporal GPU checks
at 30/60/120 Hz, including byte-exact inactive day/night output. These are scoped
material/effect results, not an all-technique visual acceptance upgrade.

**Outer column content, 2026-09-04 (dirty `d2174ed`):** seven independent damaged
columns placed with 2.5 m arena clearance, buried caps and existing arena materials.
85 structural cells and eight loose stones use the existing physical magic paths.
**2/2 EditMode and fresh 7/7 PlayMode passed**, latest UTC 10:42:17 on September 5;
every cell was grabbed and repaired, loose stones were reacquired, unsupported
islands released and foundation-connected cells stayed seated during partial repair.
[Evidence](OUTER_STONE_RING.md).
This scoped content result does not upgrade broader M11 acceptance.

Current branch: `codex/environment-aware-motion-matching-spike`

Current implementation: dirty working tree on `d2174eded114dd022e4a9c442abadda7a0e44555`.

Historical foundation evidence below belongs to `8c2e6245a0e4fe1e169b63998e918ce800477b36`,
not the current dirty tree. Current scoped evidence is tracked separately.

Technical context: [`Docs/PROJECT_TECHNICAL_STATE.md`](PROJECT_TECHNICAL_STATE.md)

## Current verdict

**MMB field + contact packing, UTC 14:14:** completed the user-clarified group
interaction. Fixed armor-release input latch; restored radius collection while
MMB is held; removed artificial orbit-slot spacing. **9/9 Edit + 9/9 focused Play
passed** on dirty `d2174ed`, including real button input for three arena cells
after armor, arrivals, contact packing, repeated group capture and rest/wake.
This supersedes the single-target assumption below; wider M11 acceptance is
unchanged. [Evidence](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Middle-button clarification, UTC 13:31:** Input System MMB press/hold/move/release
and repeated presses pass through the shipping adapter/router; **8/8 focused Play**.
The user's remaining gravity-grip failure is still unconfirmed in this scenario;
no additional gameplay change or acceptance upgrade is claimed.
[Raw-button evidence](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Arena gravity acquisition, 2026-09-04 (dirty `d2174ed`, UTC 13:25):** fixed an
additional user-reproduced failure: Surface-only arena floor intercepted MMB rays
and produced empty successful sessions. Focus now requires Gravity/Repair; start
requires captured matter or a controllable structure. New shipping-geometry
screen-point regression passes lift and repeated grabs; circle disassembly/repair
is retained. **4/4 EditMode + 7/7 focused PlayMode passed.** User wave/shadow settings
retained. The broader legacy shadow assertion failed and is documented separately;
M11/global acceptance remains unchanged. [Details](ARENA_GRAVITY_ACQUISITION_FIX.md).

**Loose stone fixes, 2026-09-04 (dirty `d2174ed`, UTC 12:48):** camera-hidden arena
parents no longer block valid loose targets; supported stones sleep without drift,
wake for grip and fall on support removal; wall/platform shards have one gravity
authority. Oblique cuts and broad bevels replace rectangular secondary fractures,
while primary arena detachment remains exact. **12/12 Edit, 5/5 loose stone Play,
3/3 production fracture Play passed**. Current saved wave settings retained by
matching backup/profile SHA256. [Evidence and baseline](LOOSE_STONE_FIX.md),
[geometry/Editor measurements](CONTAINED_FRACTURE_FIX.md). This scoped result does
not change wider-suite or M11 acceptance.

**Wave overlap and head seam stones, 2026-09-04 (dirty `d2174ed`, UTC 12:21):**
the previous automatic propagation slowdown is superseded. Each row samples one
travelling pulse at distance/speed; a new top Inspector length field scales the
visible phase times. The user's saved profile has not been retuned. Head armor
uses measured skinned geometry, final-pose attachment and 16 independent small
gap stones included in orbit and projectiles. Base body layout/profile preserved.
**23/23 EditMode and 5/5 focused PlayMode passed.** All 93 wave cells can rise
together without penetration; all 16 fillers expand and launch; head-follow
error < .000004 m. Evidence: `WaveContactEdit.json`, `WaveHeadArmorPlay.json`,
`HeadArmor/Latest.json` and `WaveContact/Latest.json` under BuildReports.
Three wider exploratory-suite failures are recorded in [HEAD_ARMOR_FIX.md](HEAD_ARMOR_FIX.md)
and remain outside this scoped result. M11/global acceptance is not upgraded.

**Wave placement and moving crest, 2026-09-04 (dirty `d2174ed`, UTC 11:43):** fixed
lowest-vertex recentering that snapped pieces sideways and caused neighbour overlap.
Shared cast frame, contained bevels and whole-wave reservation keep the partition
stable; crest height travels outward and older rows retreat. Long saved timings
limit effective speed, now labelled as a maximum in the Inspector. Dust preserves
its authored tint without lighting-driven black smoke. **21/21 Edit + 2/2 Play**:
21,646 collision pairs with zero penetration; 1.25 million projected vertices with
zero drift; advancing crest and descending rows verified; dense dust lighting delta
zero. Saved user profile unchanged. [Scope, baselines and captures](WAVE_CONTACT_FIX.md).

**Filled fracture, stable wave and contact FX, 2026-09-04 (dirty `d2174ed`):**
children now partition the original convex volume (97.62% collision / 96.39% render
fill in the rotated/recursive fixture). Live wave geometry cannot be stolen by
another cast; its ground cross-section stays fixed. Added contour dust/chip streams,
stronger extraction cues, matching small wall collider hulls and geometry-based
gravity packing. User scene, shadows and updated long wave timings are retained.
Contained fracture **8/8 Edit + 3/3 Play** (latest UTC `10:17:57`); production wave/
contact/grip **4/4 Play** (`10:22:37`), with the focused Edit report in
`BuildReports/WaveContactEdit.json`. Cached split maximum 0.248 ms / 0 managed bytes;
wave contact marker maximum 5.9642 ms in Editor. Cold preparation/whole-frame costs
remain distinct. [Details](WAVE_CONTACT_FIX.md); [fill evidence](CONTAINED_FRACTURE_FIX.md).

**Wave/arena fracture follow-up, 2026-09-04 (dirty `d2174ed`):** exact arena
detachment restored; secondary stones and wall visuals fit the real parent convex.
Repeated splitting of thin rotated sources preserves containment and mass. Wave
cells are bounded, pool mesh reuse is corrected, and authoring is reduced to six
curves, five durations and five main controls. Saved user values remain intact.
Scoped checks: **28/28 EditMode + 4/4 PlayMode** (latest UTC `09:32:31`). Physical
split measurement: maximum **0.6496 ms**, zero managed bytes across four calls.
Wave preparation maximum **168.865 ms** in the Editor remains a startup performance
risk. [Wave evidence](WAVE_REPAIR.md); [arena/stone evidence](CONTAINED_FRACTURE_FIX.md).

**Wall material correction:** replaced the entire natural-fracture material array,
removing the retained clay overlay. Saved wall interior and setup defaults now
reference sandstone; the runtime check covers every material slot.

**Combat/mobility follow-up, 2026-09-04 (dirty `d2174ed`):** cumulative armor and
structure damage, persistent secondary column breakup, natural wall stone
visuals, 3D chip rotation, surf/pillar dust, no roll on cushioned landing, and
exposed continuous wave animation are implemented with preserved user scene and
shadow settings. Current scoped evidence: [combat/mobility notes](COMBAT_MOBILITY_FIXES.md).
Final focused checks: **9/9 EditMode + 4/4 PlayMode**, UTC `08:49:26`.

**Dust/shadow follow-up, 2026-09-04 (dirty `d2174ed`):** user Sun settings saved;
cosmetic-shard/dust compositing corrected across the shared effects paths. Fresh
authoring **5/5** and pixel/production PlayMode **2/2** pass (UTC `07:37:29`).
Physical stone depth remains intact; prior orphan armor warnings remain.
See [settings and scoped evidence](EARTH_MATERIAL_PASS_CHECKLIST.md#dust-and-shard-authoring-follow-up--september-4).

**Earth material pass, 2026-09-04:** the approved 11-part implementation is integrated
through a scene-preserving menu. Production measurements confirm 96 armor shots at
44 m/s, actual charged wall-piece movement after physics, persistent medium/huge
split mass and active backward EAMM. Final category-wide bevel and effects validation
is recorded in the [material-pass checklist](EARTH_MATERIAL_PASS_CHECKLIST.md).
Do not interpret scoped smoke checks as completion of previous animation milestones.

**Mobility follow-up, 2026-09-04 (dirty working tree on `d2174ed`):** wave
foundations are lowered 20% along surface up, configurable in EarthPillarWaveProfile;
arena/planet placement and bindings tests pass (21/21 EditMode, UTC 22:15:02 Sep 3).
Narrow saved-scene repair restores the launch pillar and surf material. Combined
production regression at UTC 22:16:03 is **3/4**, not accepted as fully green:
player roll travels 2.458 m and bot 1.786 m; bot misses the 2 m distance gate.
Both now actually tumble and retain the outgoing blend. Idle/stop/mobility pass.
See technical-state follow-up for exact evidence and remaining performance/visual
limits. This supersedes the earlier incomplete roll acceptance below.

**Landing-roll travel, 2026-09-03 (working tree on `d2174ed`):** fixed-clock motor
roll motion now accompanies the authored landing roll with bounded forward decay.
11/11 pure tests and 1/1 dual-fighter production drop test passed; the subsequent
combined foot/stop/roll regression passes 3/3 at `2026-09-03T21:32:59.3335687Z`. Exact timestamps
and distances are recorded in the technical state. Re-run via `Elemental/QA/Run
Landing Roll Motion EditMode Tests` and `Elemental/QA/Run Landing Roll Motion
PlayMode Test`. Existing user clips, Blend Tree, scene and visual profiles were not
regenerated. Broad animation/obstacle/performance acceptance remains open.

**Stop/support-release follow-up, 2026-09-03 (working tree on `d2174ed`):** free
foot targets now filter only the contact correction relative to authored motion;
invalid support and lock hand-offs discard stale filter history. LateUpdate no
longer repositions/rotates ankles after the Humanoid knee solve. Seven added pure
tests reproduced 4.3–4.47 m run/stop target backlog before the fix; the updated
`AnimationTransitionsVNextEdit.json` passes 50/50 at `2026-09-03T21:16:41.3800060Z`.
`IdleFootOrientationPlay.json` passes 2/2 at `2026-09-03T21:20:53.3573992Z`, including
actual forward/back movement and stop on both actors (free-target lag <=5 mm),
Avatar-bounded shin length, and the prior idle-inversion regression. EditMode
emitted pre-existing `EarthArmorPiece` required-component warnings. Platform-edge
visual QA, EAMM pose rejection, and broad performance acceptance remain separate.

**Narrow runtime fix, 2026-09-03 (working tree on `d2174ed`):** idle feet no longer
receive the skeleton-bone orientation as their Humanoid IK goal. The live
on/off/on reproduction isolated the inversion to `EarthFootContactController`.
`BuildReports/IdleFootOrientationPlay.json` at `2026-09-03T21:09:52.6993029Z`
passes 1/1, exercising both fighters and repeated contact ramps at 30/60/120
frame-rate caps. User clips/Blend Tree/material settings are preserved. Re-run via
`Elemental/QA/Run Idle Foot Orientation Regression`. This does not close the
remaining EAMM pose rejection or the broad animation/performance gates.

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
| EAMM production animation graph and catalog | Implemented in dirty working tree | `EarthAnimationGraph`, `EarthInertializationJob`, semantic catalog, 2D locomotion, front/back recovery, bounded magic reach | No PlayMode/capture/profile run by explicit request; final Unity import after catalog bootstrap and visual acceptance remain pending. |

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

Landing follow-up (2026-09-03, dirty worktree on
`codex/environment-aware-motion-matching-spike`, HEAD `d2174eded114dd022e4a9c442abadda7a0e44555`):
the earlier user report was a collapsing short hop after style/input fixes. Ordinary
`Land` shares the full-strength hard-landing clip. The current patch adds height/impact-scaled
pose blending and excludes initial support acquisition, without changing jump physics or camera.
This is implementation evidence, not visual acceptance. Test suites and automatic Play/Stop
cycles are intentionally omitted at the user's request; short-hop and startup appearance remain
open until checked in the user's live scene.
Connected Unity MCP refresh completed with compilation/import idle and zero Console
errors/warnings; no Play mode cycle or test runner was started for this verification.
The user subsequently confirmed short jumping fixed, but reported startup falling and
unwanted low-height rolls. The follow-up seeds classified motor support before first render
and prevents initial/ordinary landing from forcing the physical knockdown/get-up path;
catastrophic fall KO and combat knockdowns remain. New startup/roll behavior is not yet
visually accepted.
The earlier roll revision admitted a fast forward/backward jump (`6.5 m/s`) or a strong
external airborne velocity change (`4 m/s`, gravity excluded), in addition to a drop `>2 m`.
That revision selected reverse roll playback for backward travel. Motor-only capsule rotation is protected during
animated control and released on motor disable; camera, jump impulse and gravity are unchanged.
Follow-up verification: new code compiles; generated backward clip is Humanoid, has `138`
curves and duration `0.7043334 s`, and is assigned to `Moving Land Back` at positive speed `1`.
The user stopped Play after a script-reload session produced motion-matching cache exceptions;
the agent baked the asset in Edit mode and did not start another Play run. Fresh runtime/visual
acceptance remains pending; old Console exceptions were not cleared or presented as a pass.

Latest correction (same date, supersedes the reverse-roll revision): the user's renewed report
was reproduced by a read-only, explicitly armed Editor startup recorder, not Test Runner.
`CharacterStartupProbe-Before.log` shows folded EAMM output at startup and after the bot's first
cast despite grounded motors, zero impacts and no ragdoll. Candidate hierarchy validation now
rejects that pose before graph output; authored locomotion remains the safe fallback.
The fresh `BuildReports/RuntimeRescue/CharacterStartupProbe.log` contains 87 samples over
5.064 seconds: hips-up projection stays 0.61–0.82, with neither original negative startup pose
nor bot's second collapse. The player receives two actual impacts at 4.438 seconds; that combat
recovery is deliberately retained, not mislabeled as startup instability. Both diagnostic Play
sessions were stopped; zero Console errors after the new run. No test suite was run.
The roll speed threshold is now 9 m/s (normal authored run speed is 7.2, above the old 6.5 gate),
and takeoff itself is excluded from external-impulse evidence. Backward landings temporarily use
ordinary landing/brace; the visually rejected synthetic reverse clip is no longer referenced by
`Moving Land Back`. The typed impact adapter also excludes classified static support contacts
from the combat impulse path. Jump/landing appearance still requires the user's in-game check;
EAMM retarget calibration itself remains unresolved, not accepted as working.
Existing source clips and download settings: `Docs/ANIMATION_INVENTORY.md`.

Runtime rescue update (2026-09-03): baked local-space EAMM, unified animation parameter routing,
camera wiring, dual-fighter DOF, and radial-gravity audits are live and visually captured in
`BuildReports/RuntimeRescue/GameView_RuntimeRescue_Final.png`. EditMode animation contracts pass
`43/43`; Earth Magic Expansion PlayMode is `16/17`, blocked only by the first-tick pillar launch
speed gate (`0.416 m/s`, required `>0.5 m/s`).

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
